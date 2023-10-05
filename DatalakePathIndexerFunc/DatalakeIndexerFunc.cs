﻿using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Web;
using Azure.Messaging.ServiceBus;
using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SearchIndexerTest;

namespace DatalakePathIndexerFunc;

public class DatalakePathIndexer
{
    private readonly ILogger _logger;
    private readonly SearchClient _searchClient;
    private readonly PathIndexClient _pathIndexClient;
    private readonly DataLakeIndexer _dataLakeIndexerDerp;
    private readonly DataLakeServiceClient _dataLakeServiceClient;

    public DatalakePathIndexer(ILoggerFactory loggerFactory, SearchClient searchClient, PathIndexClient pathIndexClient, DataLakeIndexer dataLakeIndexerDerp, DataLakeServiceClient dataLakeServiceClient)
    {
        _logger = loggerFactory.CreateLogger<DatalakePathIndexer>();
        _searchClient = searchClient;
        _pathIndexClient = pathIndexClient;
        _dataLakeIndexerDerp = dataLakeIndexerDerp;
        _dataLakeServiceClient = dataLakeServiceClient;
    }


    [Function("HandleBlobCreatedEvent")]
    public async Task HandleBlobCreatedEvent([ServiceBusTrigger("blob-created-event-queue", Connection = "ServiceBusConnection", IsBatched = true)] ServiceBusReceivedMessage[] messages)
    {
        _logger.LogInformation("Received {count} blob created events in batch", messages.Length);

        var documents = messages.Select(o =>
        {
            var body = o.Body.ToObjectFromJson<BlobEvent>();

            var (fileSystem, path) = Utils.UrlToFilesystemAndPath(body.Data.Url);

            return new PathIndexModel
            {
                filesystem = fileSystem,
                key = Convert.ToBase64String(Encoding.UTF8.GetBytes(body.Data.Url)),
                lastModified = body.EventTime,
                path = path,
            };
        }).ToImmutableList();

        try
        {
            await _searchClient.MergeOrUploadDocumentsAsync(documents);
            _logger.LogInformation("Indexed {count} paths", messages.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "something went wrong uploading to index :/");
            throw;
        }
    }


    [Function("HandleBlobDeletedEvent")]
    public async Task HandleBlobDeletedEvent([ServiceBusTrigger("blob-deleted-event-queue", Connection = "ServiceBusConnection", IsBatched = true)] ServiceBusReceivedMessage[] messages)
    {
        // hohum what exactly should we do with these? only add to blob deleted paths index?

        _logger.LogInformation("Received {count} blob deleted events in batch", messages.Length);

        var documents = messages.Select(o =>
        {
            var body = o.Body.ToObjectFromJson<BlobEvent>();

            var (fileSystem, path) = Utils.UrlToFilesystemAndPath(body.Data.Url);

            return new PathIndexModel
            {
                filesystem = fileSystem,
                key = Convert.ToBase64String(Encoding.UTF8.GetBytes(body.Data.Url)),
                lastModified = body.EventTime,
                path = path,
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



    [Function("RunIndexer")]
    public async Task RunIndexer([TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo, FunctionContext context)
    {
        _logger.LogInformation("Running indexer...");

        static async Task<SomeOtherIndexModel?> mapperFunc(PathIndexModel path, FileDownloadInfo file)
        {
            var document = await JsonSerializer.DeserializeAsync<TestIndexModel>(file.Content).ConfigureAwait(false);

            return document != null
                ? new SomeOtherIndexModel
                {
                    booleanvalue = document.booleanvalue,
                    numbervalue = document.numbervalue,
                    pathbase64 = path.key,
                    stringvalue = document.stringvalue,
                    eTag = file.Properties.ETag.ToString(),
                    pathUrlEncoded = HttpUtility.UrlEncode(path.path),
                    lastModified = path.lastModified,
                }
                : null;
        }

        var options = new ListPathsOptions { FromLastModified = new DateTimeOffset(2023, 9, 28, 5, 0, 0, TimeSpan.Zero) };  // so this should actually be the time of the last successful run

        var indexerResult = await _dataLakeIndexerDerp.RunDocumentIndexerOnPathsAsync(_dataLakeServiceClient.GetFileSystemClient("stuff-large"), _pathIndexClient.ListPathsAsync(options), mapperFunc, default);

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
