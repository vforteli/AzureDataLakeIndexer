using System.Text.Json;
using System.Web;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;
using DataLakeFileSystemClientExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchIndexerTest;

const string indexName = "someindex-large";
const string pathCreatedIndexName = "path-created-index";
const string pathDeletedIndexName = "path-deleted-index";


var config = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true).Build();

var searchServiceUri = new Uri(config.GetValue<string>("searchServiceUri") ?? throw new ArgumentNullException("searchServiceUri"));
var searchServiceCredendial = new AzureKeyCredential(config.GetValue<string>("searchServiceApiKey") ?? throw new ArgumentNullException("searchServiceApiKey"));
var datalakeConnectionString = new Uri(config.GetValue<string>("storageConnectionString") ?? throw new ArgumentNullException("storageConnectionString"));


var loggerFactory = LoggerFactory.Create(o => o.AddSimpleConsole(c => c.SingleLine = true));


using var cancellationTokenSource = new CancellationTokenSource();



Console.CancelKeyPress += (s, e) =>
{
    if (!cancellationTokenSource.IsCancellationRequested)
    {
        e.Cancel = true;
        Console.WriteLine("Breaking, waiting for queued tasks to complete. Press break again to force stop");
        cancellationTokenSource.Cancel();
    }
    else
    {
        Console.WriteLine("Terminating threads");
        Environment.Exit(1);
    }
};


var datalakeServiceClient = new DataLakeServiceClient(datalakeConnectionString);

var pathIndexClient = new PathIndexClient(new SearchClient(searchServiceUri, pathCreatedIndexName, searchServiceCredendial), loggerFactory.CreateLogger<PathIndexClient>());
var indexer = new DataLakeIndexer(new SearchClient(searchServiceUri, indexName, searchServiceCredendial), loggerFactory.CreateLogger<DataLakeIndexer>(), new DatalakeIndexerOptions());


// setup datalake stuff
var sourceFileSystemClient = datalakeServiceClient.GetFileSystemClient("stuff-large");
//await sourceFileSystemClient.CreateIfNotExistsAsync();

var analyzer = new CustomAnalyzer("foo-analyser", "keyword_v2");
analyzer.TokenFilters.Add("lowercase");

//await DataLakeIndexer.CreateIndexIfNotExistsAsync<TestIndexModel>(searchServiceUri, searchServiceCredendial, indexName);
await Utils.CreateOrUpdateIndexAsync<SomeOtherIndexModel>(searchServiceUri, searchServiceCredendial, indexName);
await Utils.CreateOrUpdateIndexAsync<PathIndexModel>(searchServiceUri, searchServiceCredendial, pathCreatedIndexName, analyzer);
await Utils.CreateOrUpdateIndexAsync<PathIndexModel>(searchServiceUri, searchServiceCredendial, pathDeletedIndexName, analyzer);


var largeSourceFileSystem = datalakeServiceClient.GetFileSystemClient("stuff-large-files");
await largeSourceFileSystem.CreateIfNotExistsAsync();

// await DataLakeWriter.WriteLongerStuff(largeSourceFileSystem);

// testing filterint with larger index...
//await pathIndexClient.UploadTestPathsAsync("doesntexist", DataLakeWriter.GeneratePaths(1000, 100, 100));

// await pathIndexClient.RebuildPathsIndexAsync(largeSourceFileSystem.ListPathsParallelAsync("/"), largeSourceFileSystem.Name);

// return;


var documentCountResult = await new SearchClient(searchServiceUri, indexName, searchServiceCredendial).GetDocumentCountAsync();
Console.WriteLine(documentCountResult.Value);


Console.WriteLine("Running indexer...");
var options = new ListPathsOptions
{
    FromLastModified = new DateTimeOffset(2023, 9, 28, 5, 0, 0, TimeSpan.Zero),
    Filter = "search.ismatch('partition*')",
    // Filter = "filesystem eq 'stuff-large-files'"
};

Func<PathIndexModel, FileDownloadInfo, Task<SomeOtherIndexModel?>> somefunc = async (path, file) =>
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
            pathUrlEncoded = path.pathUrlEncoded,
            lastModified = path.lastModified,
        }
        : null;
};


Console.WriteLine("Press enter to start...");
Console.ReadLine();
var indexerResult = await indexer.RunDocumentIndexerOnPathsAsync(datalakeServiceClient, pathIndexClient.ListPathsAsync(options), somefunc, cancellationTokenSource.Token);

Console.WriteLine(JsonSerializer.Serialize(indexerResult));