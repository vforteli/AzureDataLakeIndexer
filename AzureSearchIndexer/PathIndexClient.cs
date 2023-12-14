using System.Collections.Immutable;
using System.Diagnostics;
using System.Web;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;

namespace AzureSearchIndexer;

/// <summary>
/// Wraps a search client with methods for managing the path index
/// </summary>
public class PathIndexClient(SearchClient pathIndexSearchClient, ILogger<PathIndexClient> logger)
{
    private const int LogIntervalMilliSeconds = 5000;
    private const int SearchPageSize = 5000;  // this seems to yield the best performance in some not very scientific tests


    /// <summary>
    /// Upsert paths to path index
    /// </summary>
    public virtual async Task<UpsertPathsResult> UpsertPathsAsync(ImmutableList<PathIndexModel> paths)
    {
        try
        {
            var response = await pathIndexSearchClient.MergeOrUploadDocumentsAsync(paths).ConfigureAwait(false);

            var result = new UpsertPathsResult
            {
                Created = response.Value.Results.Count(o => o.Status == 201),
                Modified = response.Value.Results.Count(o => o.Status == 200),
                Failed = response.Value.Results.Count(o => o.Status >= 400),
            };

            logger.LogInformation("Status: {status}, created: {created}, modified: {modified}, failed: {failed}", response.GetRawResponse().Status, result.Created, result.Modified, result.Failed);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Something went wrong uploading to path index :/");
            throw;
        }
    }


    /// <summary>
    /// List paths from index
    /// </summary>
    public virtual async IAsyncEnumerable<PathIndexModel> ListPathsAsync(ListPathsOptions options)
    {
        logger.LogInformation("Getting paths...");

        var lastModifiedFilter = options.FromLastModified.HasValue ? $"lastModified ge {options.FromLastModified:o}" : "";

        var count = 0;

        var stopwatch = Stopwatch.StartNew();
        await using var loggingTimer = new Timer(o =>
        {
            logger.LogInformation("Found {count} documents after {elapsedSeconds} seconds, dps: {dps}", count, Math.Round(stopwatch.Elapsed.TotalSeconds), Math.Round(count / stopwatch.Elapsed.TotalSeconds));
        }, null, LogIntervalMilliSeconds, LogIntervalMilliSeconds);


        var orderByFilter = "";

        while (true)    // oh well, function will be terminated at some point anyway if the previousKey filter fails...
        {
            var searchOptions = new SearchOptions
            {
                Filter = Utils.ConcatWithAnd(orderByFilter, lastModifiedFilter, options.Filter),
                Size = SearchPageSize,
            };
            searchOptions.OrderBy.Add("key");

            string? previousKey = null;
            await foreach (var path in (await pathIndexSearchClient.SearchAsync<PathIndexModel>("", searchOptions).ConfigureAwait(false)).Value.GetResultsAsync())
            {
                count++;
                yield return path.Document;

                previousKey = path.Document.key;
            }

            if (previousKey == null)
            {
                logger.LogInformation("Done. Found {count} documents after {elapsedSeconds} seconds, dps: {dps}", count, Math.Round(stopwatch.Elapsed.TotalSeconds), Math.Round(count / stopwatch.Elapsed.TotalSeconds));
                yield break;
            }

            orderByFilter = $"key gt '{previousKey}'";
        }
    }


    /// <summary>
    /// Rebuild the path index by listing all files in specified path
    /// </summary>
    public virtual async Task<UpsertPathsResult> RebuildPathsIndexAsync(IAsyncEnumerable<PathItem> paths, string sourceFileSystemName)
    {
        const int bufferSize = 1000;

        long created = 0;
        long modified = 0;
        long failed = 0;

        var buffer = new List<PathItem>(bufferSize);

        var pathsEnumerator = paths.GetAsyncEnumerator();
        while (true)
        {
            var hasCurrent = await pathsEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (hasCurrent && (!pathsEnumerator.Current.IsDirectory ?? false))
            {
                buffer.Add(pathsEnumerator.Current);
            }

            if (buffer.Count == bufferSize || (!hasCurrent && buffer.Count > 0))
            {
                var now = DateTime.UtcNow;
                var result = await UpsertPathsAsync(buffer.Select(o => new PathIndexModel
                {
                    filesystem = sourceFileSystemName,
                    fileLastModified = o.LastModified,
                    lastModified = now,
                    pathUrlEncoded = HttpUtility.UrlEncode(o.Name),
                }).ToImmutableList()).ConfigureAwait(false);

                created += result.Created;
                modified += result.Modified;
                failed += result.Failed;

                buffer.Clear();
            }

            if (!hasCurrent)
            {
                break;
            }
        }

        return new UpsertPathsResult
        {
            Created = created,
            Modified = modified,
            Failed = failed,
        };
    }
}
