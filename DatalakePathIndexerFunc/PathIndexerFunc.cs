using System.Collections.Immutable;
using System.Web;
using Azure.Messaging.ServiceBus;
using AzureSearchIndexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DatalakePathIndexerFunc;

/// <summary>
/// Func for updating the path index from datalake events
/// </summary>
public class PathIndexerFunc(ILoggerFactory loggerFactory, PathIndexClient pathIndexClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PathIndexerFunc>();

    [Function("HandleBlobCreatedEvent")]
    public async Task HandleBlobCreatedEvent([ServiceBusTrigger("blob-created-event-queue", Connection = "ServiceBusConnection", IsBatched = true)] ServiceBusReceivedMessage[] messages)
    {
        _logger.LogInformation("Received {count} blob created events in batch", messages.Length);

        var now = DateTime.UtcNow;
        await pathIndexClient.UpsertPathsAsync(messages.Select(o =>
        {
            var body = o.Body.ToObjectFromJson<BlobEvent>();
            var (fileSystem, path) = Utils.UrlToFilesystemAndPath(body.Data.Url);

            return new PathIndexModel
            {
                filesystem = fileSystem,
                fileLastModified = body.EventTime,
                lastModified = now,
                pathUrlEncoded = HttpUtility.UrlEncode(path),
            };
        }).ToImmutableList());
    }


    [Function("HandleBlobDeletedEvent")]
    public async Task HandleBlobDeletedEvent([ServiceBusTrigger("blob-deleted-event-queue", Connection = "ServiceBusConnection", IsBatched = true)] ServiceBusReceivedMessage[] messages)
    {
        // hohum what exactly should we do with these? only add to blob deleted paths index?

        _logger.LogInformation("Received {count} blob deleted events in batch", messages.Length);

        var now = DateTime.UtcNow;
        var documents = messages.Select(o =>
        {
            var body = o.Body.ToObjectFromJson<BlobEvent>();

            var (fileSystem, path) = Utils.UrlToFilesystemAndPath(body.Data.Url);

            return new PathIndexModel
            {
                filesystem = fileSystem,
                fileLastModified = body.EventTime,
                lastModified = now,
                pathUrlEncoded = HttpUtility.UrlEncode(path),
            };
        }).ToImmutableList();

        await Task.CompletedTask;
        _logger.LogInformation("do something...");
        //try
        //{
        //    await _searchClient.MergeOrUploadDocumentsAsync(documents);
        //    _logger.LogInformation("Indexed {count} paths", messages.Length);
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "something went wrong uploading to index :/");
        //    throw;
        //}
    }
}
