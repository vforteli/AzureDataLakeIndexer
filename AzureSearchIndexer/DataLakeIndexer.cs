using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

public class DataLakeIndexer(SearchClient searchClient, ILogger<DataLakeIndexer> logger)
{
    private const int MaxReadThreads = 128;
    private const int MaxUploadThreads = 4; // this should possibly be set to the number of search units? have to check
    private const int DocumentBatchSize = 1000;    // according to documentation this is the max batch size... although it seems to work with higher values... they dont bring performance benefits though


    /// <summary>
    /// Run document indexer with a function for mapping 
    /// </summary>
    public async Task<IndexerRunMetrics> RunDocumentIndexerOnPathsAsync<TIndex>(DataLakeServiceClient dataLakeServiceClient, IAsyncEnumerable<PathIndexModel> paths, Func<PathIndexModel, FileDownloadInfo, Task<TIndex?>> func, CancellationToken cancellationToken)
    {
        var pathsBuffer = new BlockingCollection<PathIndexModel>(DocumentBatchSize * MaxUploadThreads * 2);

        var listPathsTask = Task.Run(async () =>
        {
            await foreach (var path in paths)
            {
                pathsBuffer.Add(path);
            }

            pathsBuffer.CompleteAdding();
        }, cancellationToken);

        long totalSize = 0;
        var documents = new BlockingCollection<TIndex>(DocumentBatchSize * (MaxUploadThreads + 2));
        var batchingUploader = new BatchingUploader(logger, MaxUploadThreads, DocumentBatchSize);

        var documentReadCount = 0;
        var documentReadFailedCount = 0;

        var stopwatch = Stopwatch.StartNew();

        var readDocumentsTask = Task.Run(async () =>
        {
            await using var timer = new Timer(s => { logger.LogInformation("Read {documentsReadCount} documents... {dps} fps", documentReadCount, documentReadCount / (stopwatch.ElapsedMilliseconds / 1000f)); }, null, 3000, 3000);

            var readTasks = new ConcurrentDictionary<Guid, Task>();

            try
            {
                using var semaphore = new SemaphoreSlim(MaxReadThreads, MaxReadThreads);

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
                                Interlocked.Add(ref totalSize, await Utils.GetJsonLengthAsync(document));
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
        }, cancellationToken);

        var uploadDocumentsTask = batchingUploader.UploadBatchesAsync(documents, searchClient, cancellationToken);

        await Task.WhenAll(readDocumentsTask, uploadDocumentsTask).ConfigureAwait(false);
        logger.LogInformation("Indexing done, took {elapsed}", stopwatch.Elapsed);
        logger.LogInformation("Total size of documents in json form: {size}", totalSize);

        return new IndexerRunMetrics
        {
            DocumentReadCount = documentReadCount,
            DocumentReadFailedCount = documentReadFailedCount,
            DocumentUploadCount = uploadDocumentsTask.Result.UploadCount,
            DocumentUploadCreatedCount = uploadDocumentsTask.Result.CreatedCount,
            DocumentUploadFailedCount = uploadDocumentsTask.Result.FailedCoumt,
            DocumentUploadModifiedCount = uploadDocumentsTask.Result.ModifiedCount,
        };
    }
}
