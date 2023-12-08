using System.Text.Json;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AzureSearchIndexer;

public static class Utils
{
    public static (string fileSystem, string path) UrlToFilesystemAndPath(string url)
    {
        var parts = url.Split('/', 5);
        return (parts[3], parts[4]);
    }

    public static string ConcatWithAnd(params string?[] args) =>
        string.Join(" and ", args.Where(o => !string.IsNullOrEmpty(o)));


    /// <summary>
    /// Create or update an index
    /// </summary>
    public static async Task CreateOrUpdateIndexAsync<T>(Uri searchServiceUri, AzureKeyCredential credential, string indexName, CustomAnalyzer? analyzer = null)
    {
        var searchIndexClient = new SearchIndexClient(searchServiceUri, credential);

        try
        {
            var index = new SearchIndex(indexName, new FieldBuilder().Build(typeof(T)));

            if (analyzer != null)
            {
                index.Analyzers.Add(analyzer);
            }

            var result = await searchIndexClient.CreateOrUpdateIndexAsync(index);
            Console.WriteLine($"Index create or update status: {result.GetRawResponse().Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to create or update index {indexName}");
            Console.WriteLine(ex);
        }
    }


    /// <summary>
    /// Get the length of the object serialized to json using STJ without allocating too much additional memory
    /// </summary>
    public static async Task<long> GetJsonLengthAsync(object thing, JsonSerializerOptions? options = default, CancellationToken token = default)
    {
        var stream = new CountingStream();
        await JsonSerializer.SerializeAsync(stream, thing, options, token);
        return stream.Length;
    }
}
