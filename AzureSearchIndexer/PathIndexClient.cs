using System.Diagnostics;
using Azure.Search.Documents;
using AzureSearchIndexer;
using Microsoft.Extensions.Logging;

namespace SearchIndexerTest;

public class PathIndexClient
{
    private readonly SearchClient _pathIndexSearchClient;
    private readonly ILogger _logger;
    private const int logIntervalMilliSeconds = 5000;
    private const int size = 5000;  // this seems to yield the best performance in some not very scientific tests


    public PathIndexClient(SearchClient pathIndexSearchClient, ILogger<PathIndexClient> logger)
    {
        _pathIndexSearchClient = pathIndexSearchClient;
        _logger = logger;
    }


    /// <summary>
    /// List paths from index
    /// </summary>
    public async IAsyncEnumerable<PathIndexModel> ListPathsAsync(ListPathsOptions options)
    {
        _logger.LogInformation("Getting paths...");

        var lastModifiedFilter = options.FromLastModified.HasValue ? $"lastModified ge {options.FromLastModified:o}" : "";

        var count = 0;

        var stopwatch = Stopwatch.StartNew();
        using var loggingTimer = new Timer(o =>
        {
            _logger.LogInformation("Found {count} documents after {elapsedSeconds} seconds, dps: {dps}", count, Math.Round(stopwatch.Elapsed.TotalSeconds), Math.Round(count / stopwatch.Elapsed.TotalSeconds));
        }, null, logIntervalMilliSeconds, logIntervalMilliSeconds);


        var orderByFilter = "";

        while (true)    // oh well, function will be terminated at some point anyway if the previousKey filter fails...
        {
            var searchOptions = new SearchOptions
            {
                Filter = Utils.ConcatWithAnd(orderByFilter, lastModifiedFilter, options.Filter),
                Size = size,
            };
            searchOptions.OrderBy.Add("key");

            string? previousKey = null;
            await foreach (var path in (await _pathIndexSearchClient.SearchAsync<PathIndexModel>("", searchOptions)).Value.GetResultsAsync())
            {
                count++;
                yield return path.Document;

                previousKey = path.Document.key;
            }

            if (previousKey == null)
            {
                _logger.LogInformation("Done. Found {count} documents after {elapsedSeconds} seconds, dps: {dps}", count, Math.Round(stopwatch.Elapsed.TotalSeconds), Math.Round(count / stopwatch.Elapsed.TotalSeconds));
                yield break;
            }

            orderByFilter = $"key gt '{previousKey}'";
        }
    }
}

public record ListPathsOptions
{
    /// <summary>
    /// OData filter for eg running indexer with partitions by filtering to certain paths etc
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// Set to filter paths where last modified is greater or equal to value
    /// Should usually be set to the time of the last successful indexer run
    /// </summary>
    public DateTimeOffset? FromLastModified { get; init; }
}