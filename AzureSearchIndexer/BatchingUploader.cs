using System.Collections.Concurrent;
using System.Collections.Immutable;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

public record BatchingUploader(ILogger logger, int maxUploadThreads, int maxBatchCount, long maxBatchSizeBytes = 63 * 1024 * 1024)
{
    public virtual async Task<UploadMetrics> UploadBatchesAsync<TIndex>(BlockingCollection<TIndex> documents, SearchClient searchClient, CancellationToken cancellationToken = default)
    {
        var processedCount = 0;
        var failedCount = 0;
        var ignoredTooLargeCount = 0;
        var createdCount = 0;
        var modifiedCount = 0;

        var sendTasks = new ConcurrentDictionary<Guid, Task>();
        var buffer = new List<(long size, TIndex document)>(maxBatchCount);

        using var semaphore = new SemaphoreSlim(maxUploadThreads, maxUploadThreads);

        async Task UploadBatchAsync(IImmutableList<TIndex> batch, Guid taskId)
        {
            try
            {
                var currentCount = Interlocked.Add(ref processedCount, batch.Count);
                logger.LogInformation("Sending batch with {bufferCount} documents, total: {currentCount}", batch.Count, currentCount);

                // the search client already has built in retry logic... so no point adding some here
                var response = await searchClient.UploadDocumentsAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);

                Interlocked.Add(ref createdCount, response.Value.Results.Count(o => o.Status == 201));
                Interlocked.Add(ref modifiedCount, response.Value.Results.Count(o => o.Status == 200));
                Interlocked.Add(ref failedCount, response.Value.Results.Count(o => o.Status >= 400));

                logger.LogInformation("Status: {status} for batch at {currentCount}", response.GetRawResponse().Status, currentCount);
            }
            catch (Exception ex)
            {
                // if we get here we will just have to assume the whole batch failed...
                Interlocked.Add(ref failedCount, batch.Count);
                logger.LogError(ex, $"Uh oh, sending batch failed...");
            }
            finally
            {
                sendTasks.TryRemove(taskId, out _);
                semaphore.Release();
            }
        };

        async Task CreateBatchAsync()
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            var taskId = Guid.NewGuid();
            sendTasks.TryAdd(taskId, UploadBatchAsync(buffer.Select(o => o.document).ToImmutableList(), taskId));
            buffer.Clear();
        }

        await using var timer = new Timer(s => { logger.LogInformation("Uploaded documents: created: {created}, modified: {modified}, failed: {failed}", createdCount, modifiedCount, failedCount); }, null, 3000, 3000);

        while (documents.TryTake(out var document, -1, cancellationToken) && document != null)
        {
            var currentDocumentSizeBytes = await Utils.GetJsonLengthAsync(document, token: cancellationToken).ConfigureAwait(false);

            if (currentDocumentSizeBytes > maxBatchSizeBytes)
            {
                Interlocked.Increment(ref ignoredTooLargeCount);
                logger.LogWarning("Found document larger than max batch size: {path}", "uh.. yea"); // todo ugh, do we really want to have the path here as well so we can log the failing documents... guess so
            }
            else
            {
                if (buffer.Sum(o => o.size) + currentDocumentSizeBytes > maxBatchSizeBytes) // so wasteful... anyway
                {
                    await CreateBatchAsync().ConfigureAwait(false);
                }

                buffer.Add((currentDocumentSizeBytes, document));
            }


            if (buffer.Count == maxBatchCount || (documents.IsCompleted && buffer.Count > 0))   // actually this should also check the size of the batch, it should be max 1000 items or 16 MB
            {
                await CreateBatchAsync().ConfigureAwait(false);
            }
        }

        await Task.WhenAll(sendTasks.Select(o => o.Value)).ConfigureAwait(false);

        return new UploadMetrics(processedCount, failedCount, createdCount, modifiedCount, ignoredTooLargeCount);
    }
}
