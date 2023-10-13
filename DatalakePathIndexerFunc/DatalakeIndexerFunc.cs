using Azure.Storage.Files.DataLake;
using AzureSearchIndexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DatalakePathIndexerFunc;

/// <summary>
/// Func for doing the actual indexing of documents
/// </summary>
public class DatalakePathIndexer
{
    private readonly ILogger _logger;
    private readonly PathIndexClient _pathIndexClient;
    private readonly DataLakeIndexer _dataLakeIndexer;
    private readonly DataLakeServiceClient _dataLakeServiceClient;

    public DatalakePathIndexer(ILoggerFactory loggerFactory, PathIndexClient pathIndexClient, DataLakeIndexer dataLakeIndexerDerp, DataLakeServiceClient dataLakeServiceClient)
    {
        _logger = loggerFactory.CreateLogger<DatalakePathIndexer>();
        _pathIndexClient = pathIndexClient;
        _dataLakeIndexer = dataLakeIndexerDerp;
        _dataLakeServiceClient = dataLakeServiceClient;
    }


    // todo other triggers? should be able to run scheduled with partitions etc...
    // the big question is, where to keep the run logs... but this depends a bit on context

    [Function("RunIndexer")]
    public async Task RunIndexer([TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo, FunctionContext context) => await RunIndexerAsync(context.CancellationToken);


    public async Task RunIndexerAsync(CancellationToken token)
    {
        _logger.LogInformation("Running indexer...");

        // so this should actually be the time of the last successful run        
        var paths = _pathIndexClient.ListPathsAsync(new ListPathsOptions
        {
            FromLastModified = new DateTimeOffset(2023, 9, 28, 5, 0, 0, TimeSpan.Zero),
            Filter = "search.ismatch('partition_43*')",
        });
        var indexerResult = await _dataLakeIndexer.RunDocumentIndexerOnPathsAsync(_dataLakeServiceClient, paths, IndexMapper.MapSomethingToSomethingElseAsync, token);

        _logger.LogInformation(
            "Indexer done, documents read: {created}, failed: {failed}",
            indexerResult.DocumentReadCount,
            indexerResult.DocumentReadFailedCount);

        _logger.LogInformation(
            "Indexer done, created: {created}, modified: {modified}, failed: {failed}",
            indexerResult.DocumentUploadCreatedCount,
            indexerResult.DocumentUploadModifiedCount,
            indexerResult.DocumentUploadFailedCount);
    }
}
