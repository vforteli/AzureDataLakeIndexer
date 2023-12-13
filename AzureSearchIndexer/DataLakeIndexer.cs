using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

public class DataLakeIndexer(SearchClient searchClient, ILogger<DataLakeIndexer> logger, DatalakeIndexerOptions options)
{
    private readonly BatchingUploader batchingUploader = new BatchingUploader(logger, options.MaxUploadThreads, options.DocumentBatchSize, options.MaxDocumentBatchSizeBytes);


    /// <summary>
    /// Run document indexer with a function for mapping 
    /// </summary>
    public async Task<IndexerRunMetrics> RunDocumentIndexerOnPathsAsync<TIndex>(
        DataLakeServiceClient dataLakeServiceClient,
        IAsyncEnumerable<PathIndexModel> paths,
        Func<PathIndexModel, FileDownloadInfo, Task<TIndex?>> func,
        CancellationToken cancellationToken)
    {
        var pathsBuffer = new BlockingCollection<PathIndexModel>(options.DocumentBatchSize * options.MaxUploadThreads * 2);
        var documents = new BlockingCollection<TIndex>(options.DocumentBatchSize * (options.MaxUploadThreads + 2));

        var listPathsTask = ReadPathsAsync(paths, pathsBuffer, cancellationToken);
        var readDocumentsTask = ReadDocumentsAsync(options, dataLakeServiceClient, func, pathsBuffer, documents, cancellationToken);
        var uploadDocumentsTask = batchingUploader.UploadBatchesAsync(documents, searchClient, cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        await Task.WhenAll(listPathsTask, readDocumentsTask, uploadDocumentsTask).ConfigureAwait(false);

        logger.LogInformation("Indexing done, took {elapsed}", stopwatch.Elapsed);

        return new IndexerRunMetrics
        {
            ReadCount = readDocumentsTask.Result.ReadCount,
            ReadFailedCount = readDocumentsTask.Result.ReadFailedCount,
            ProcessedCount = uploadDocumentsTask.Result.FailedCount,
            UploadCreatedCount = uploadDocumentsTask.Result.CreatedCount,
            UploadFailedCount = uploadDocumentsTask.Result.FailedCount,
            UploadFailedTooLargeCount = uploadDocumentsTask.Result.FailedTooLargeCount,
            UploadModifiedCount = uploadDocumentsTask.Result.ModifiedCount,
        };
    }


    /// <summary>
    /// Read documents from datalake into buffer
    /// </summary>   
    internal Task<ReadDocumentsMetrics> ReadDocumentsAsync<TIndex>(
        DatalakeIndexerOptions options,
        DataLakeServiceClient dataLakeServiceClient,
        Func<PathIndexModel, FileDownloadInfo, Task<TIndex?>> func,
        BlockingCollection<PathIndexModel> pathsBuffer,
        BlockingCollection<TIndex> documents,
        CancellationToken cancellationToken) => Task.Run(async () =>
        {
            var documentReadCount = 0;
            var documentReadFailedCount = 0;
            var readTasks = new ConcurrentDictionary<Guid, Task>();
            var stopwatch = Stopwatch.StartNew();

            await using var timer = new Timer(s => { logger.LogInformation("Read {documentsReadCount} documents... {dps} fps", documentReadCount, documentReadCount / (stopwatch.ElapsedMilliseconds / 1000f)); }, null, 3000, 3000);

            try
            {
                using var semaphore = new SemaphoreSlim(options.MaxReadThreads, options.MaxReadThreads);

                while (pathsBuffer.TryTake(out var path, Timeout.Infinite, cancellationToken))
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var taskId = Guid.NewGuid();
                    readTasks.TryAdd(taskId, Task.Run(async () =>
                    {
                        try
                        {
                            var file = await dataLakeServiceClient.GetFileSystemClient(path.filesystem).GetFileClient(WebUtility.UrlDecode(path.pathUrlEncoded)).ReadAsync(cancellationToken).ConfigureAwait(false);

                            var document = await func.Invoke(path, file.Value).ConfigureAwait(false);
                            if (document != null)
                            {
                                documents.Add(document);
                                Interlocked.Increment(ref documentReadCount);
                            }
                        }
                        catch (TaskCanceledException) { }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref documentReadFailedCount);
                            logger.LogError(ex, "Failed deserializing document {path}", path.pathUrlEncoded);
                        }
                        finally
                        {
                            readTasks.TryRemove(taskId, out _);
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(readTasks.Select(o => o.Value)).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Something went horribly wrong reading documents");
            }
            finally
            {
                documents.CompleteAdding();
            }

            return new ReadDocumentsMetrics
            {
                ReadCount = documentReadCount,
                ReadFailedCount = documentReadFailedCount,
            };
        }, cancellationToken);


    /// <summary>
    /// Fill BlockingCollection with paths
    /// </summary>   
    internal static Task ReadPathsAsync(IAsyncEnumerable<PathIndexModel> paths, BlockingCollection<PathIndexModel> pathsBuffer, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            await foreach (var path in paths)
            {
                pathsBuffer.Add(path);
            }

            pathsBuffer.CompleteAdding();
        }, cancellationToken);
}
