using System.Collections.Concurrent;
using System.Collections.Immutable;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

public record UploadMetrics(int UploadCount, int FailedCoumt, int CreatedCount, int ModifiedCount);


public record BatchingUploader(ILogger logger, int maxUploadThreads, int maxBatchCount, long maxBatchSizeBytes = 64 * 1024 * 1024)
{
    public virtual async Task<UploadMetrics> UploadBatchesAsync<TIndex>(BlockingCollection<TIndex> documents, SearchClient searchClient, CancellationToken cancellationToken = default)
    {
        var documentUploadCount = 0;
        var documentUploadFailedCount = 0;
        var documentUploadCreatedCount = 0;
        var documentUploadModifiedCount = 0;

        long currentBatchSizeBytes = 0;

        var sendTasks = new ConcurrentDictionary<Guid, Task>();
        var buffer = new List<TIndex>(maxBatchCount);

        using var semaphore = new SemaphoreSlim(maxUploadThreads, maxUploadThreads);

        Task UploadBatchAsync(IImmutableList<TIndex> batch, Guid taskId) => Task.Run(async () =>
        {
            try
            {
                var currentCount = Interlocked.Add(ref documentUploadCount, batch.Count);
                logger.LogInformation("Sending batch with {bufferCount} documents, size: {size}, total: {currentCount}", batch.Count, currentBatchSizeBytes, currentCount);

                // todo handle retries? failed documents?
                var response = await searchClient.UploadDocumentsAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);

                Interlocked.Add(ref documentUploadCreatedCount, response.Value.Results.Count(o => o.Status == 201));
                Interlocked.Add(ref documentUploadModifiedCount, response.Value.Results.Count(o => o.Status == 200));
                Interlocked.Add(ref documentUploadFailedCount, response.Value.Results.Count(o => o.Status >= 400));

                logger.LogInformation("Status: {status} for batch at {currentCount}", response.GetRawResponse().Status, currentCount);
            }
            catch (Exception ex)
            {
                Interlocked.Add(ref documentUploadFailedCount, batch.Count);
                logger.LogError(ex, $"Uh oh, sending batch failed...");
            }
            finally
            {
                sendTasks.TryRemove(taskId, out _);
                semaphore.Release();
            }
        }, cancellationToken);

        await using var timer = new Timer(s => { logger.LogInformation("Uploaded documents: created: {created}, modified: {modified}, failed: {failed}", documentUploadCreatedCount, documentUploadModifiedCount, documentUploadFailedCount); }, null, 3000, 3000);

        while (!documents.IsCompleted)
        {
            if (documents.TryTake(out var document, -1, cancellationToken))
            {
                if (document != null)  // todo dahek, why is would document be null here?
                {
                    var currentDocumentSize = await Utils.GetJsonLengthAsync(document, token: cancellationToken).ConfigureAwait(false);

                    if (currentDocumentSize > maxBatchSizeBytes)
                    {
                        Interlocked.Increment(ref documentUploadFailedCount);
                        logger.LogWarning("Found document larger than max batch size: {path}", "uh.. yea"); // todo ugh, do we really want to have the path here as well so we can log the failing documents... guess so
                    }
                    else
                    {
                        currentBatchSizeBytes += currentDocumentSize;

                        if (currentBatchSizeBytes > maxBatchSizeBytes)
                        {
                            var taskId = Guid.NewGuid();
                            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                            sendTasks.TryAdd(taskId, UploadBatchAsync(buffer.ToImmutableList(), taskId));
                            buffer.Clear();
                            currentBatchSizeBytes = currentDocumentSize;
                        }

                        buffer.Add(document);
                    }
                }
            }

            if (buffer.Count == maxBatchCount || (documents.IsCompleted && buffer.Count > 0))   // actually this should also check the size of the batch, it should be max 1000 items or 16 MB
            {
                var taskId = Guid.NewGuid();
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                sendTasks.TryAdd(taskId, UploadBatchAsync(buffer.ToImmutableList(), taskId));
                buffer.Clear();
                currentBatchSizeBytes = 0;
            }
        }

        await Task.WhenAll(sendTasks.Select(o => o.Value)).ConfigureAwait(false);

        return new UploadMetrics(documentUploadCount, documentUploadFailedCount, documentUploadCreatedCount, documentUploadModifiedCount);
    }
}
