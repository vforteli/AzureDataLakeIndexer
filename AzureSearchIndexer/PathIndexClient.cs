using System.Collections.Immutable;
using System.Diagnostics;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using DataLakeFileSystemClientExtension;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

/// <summary>
/// Wraps a search client with methods for managing the path index
/// </summary>
public class PathIndexClient(SearchClient pathIndexSearchClient, ILogger<PathIndexClient> logger)
{
    private readonly SearchClient _pathIndexSearchClient = pathIndexSearchClient;
    private readonly ILogger _logger = logger;
    private const int logIntervalMilliSeconds = 5000;
    private const int size = 5000;  // this seems to yield the best performance in some not very scientific tests


    /// <summary>
    /// Upsert paths to path index
    /// </summary>
    public async Task<UpsertPathsResult> UpsertPathsAsync(ImmutableList<PathIndexModel> paths)
    {
        try
        {
            var response = await _pathIndexSearchClient.MergeOrUploadDocumentsAsync(paths);

            var result = new UpsertPathsResult
            {
                Created = response.Value.Results.Count(o => o.Status == 201),
                Modified = response.Value.Results.Count(o => o.Status == 200),
                Failed = response.Value.Results.Count(o => o.Status >= 400),
            };

            _logger.LogInformation("Status: {status}, created: {created}, modified: {modified}, failed: {failed}", response.GetRawResponse().Status, result.Created, result.Modified, result.Failed);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong uploading to path index :/");
            throw;
        }
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


    /// <summary>
    /// Rebuild the path index by listing all files in specified path
    /// </summary>
    public async Task<UpsertPathsResult> RebuildPathsIndexAsync(DataLakeFileSystemClient sourceFileSystemClient, string sourcePath)
    {
        long created = 0;
        long modified = 0;
        long failed = 0;

        var buffer = new List<PathItem>();
        await foreach (var path in sourceFileSystemClient.ListPathsParallelAsync(sourcePath))
        {
            if (!path.IsDirectory ?? false)
            {
                buffer.Add(path);
            }
            if (buffer.Count == 1000)
            {
                var result = await UpsertPathsAsync(buffer.Select(o => new PathIndexModel
                {
                    filesystem = sourceFileSystemClient.Name,
                    lastModified = o.LastModified,
                    path = o.Name,
                }).ToImmutableList());

                created += result.Created;
                modified += result.Modified;
                failed += result.Failed;

                buffer.Clear();
            }
        }

        if (buffer.Any())
        {
            await UpsertPathsAsync(buffer.Select(o => new PathIndexModel
            {
                filesystem = sourceFileSystemClient.Name,
                lastModified = o.LastModified,
                path = o.Name,
            }).ToImmutableList());
        }

        return new UpsertPathsResult
        {
            Created = created,
            Modified = modified,
            Failed = failed,
        };
    }
}

public record UpsertPathsResult
{
    required public long Created { get; init; }
    required public long Modified { get; init; }
    required public long Failed { get; init; }
}