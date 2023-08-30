using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Files.DataLake;
using DataLakeFileSystemClientExtension;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

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

var stopwatch = Stopwatch.StartNew();

var connectionString = "derp";
var sourceFileSystemClient = new DataLakeServiceClient(new Uri(connectionString)).GetFileSystemClient("stuff");

var indexName = "someindex";
var searchServiceUrl = "https://foo.search.windows.net";
var searchServiceApiKey = "foo";


var searchIndexClient = new SearchIndexClient(new Uri(searchServiceUrl), new AzureKeyCredential(searchServiceApiKey));

//await searchIndexClient.CreateIndexAsync(new Azure.Search.Documents.Indexes.Models.SearchIndex(indexName, new FieldBuilder().Build(typeof(TestIndexModel))));


var searchClient = new SearchClient(new Uri(searchServiceUrl), indexName, new AzureKeyCredential(searchServiceApiKey));


var batch = new List<TestIndexModel>(1000);

var processedCount = 0;
await foreach (var path in sourceFileSystemClient.ListPathsParallelAsync("/", cancellationToken: cancellationTokenSource.Token))
{
    if ((!path.IsDirectory ?? false) && path.Name.EndsWith(".json"))
    {
        var file = await sourceFileSystemClient.GetFileClient(path.Name).ReadAsync();
        var document = await JsonSerializer.DeserializeAsync<TestIndexModel>(file.Value.Content);

        if (document != null)
        {
            batch.Add(document with { pathbase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(path.Name)) });
        }


        // this could also handle the paths in parallel
        var currentCount = Interlocked.Increment(ref processedCount);
        if (currentCount % 10 == 0)
        {
            await searchClient.MergeOrUploadDocumentsAsync(batch);
            batch.Clear();
            Console.WriteLine($"Processed {currentCount} files and directories... {processedCount / (stopwatch.ElapsedMilliseconds / 1000f)} fps");
        }
    }
}

if (batch.Any())
{
    await searchClient.MergeOrUploadDocumentsAsync(batch);
}

Console.WriteLine($"Done, took {stopwatch.Elapsed}");
Console.WriteLine($"Found {processedCount} files and directories");


public record TestIndexModel
{
    [SimpleField(IsKey = true)]
    public string pathbase64 { get; init; } = "";

    [SearchableField]
    public string stringvalue { get; init; } = "";

    [SimpleField(IsFacetable = true, IsFilterable = true)]
    public int numbervalue { get; init; }

    [SimpleField(IsFacetable = true, IsFilterable = true)]
    public bool booleanvalue { get; init; }
}


/*{
"stringvalue": "this contains some text... yaaaay thanks for the fish etc",
"numbervalue": 42,
"booleanvalue": true
}*/