﻿using Azure;
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
    public static async Task CreateOrUpdateIndexAsync<T>(Uri searchServiceUri, AzureKeyCredential credential, string indexName)
    {
        var searchIndexClient = new SearchIndexClient(searchServiceUri, credential);

        try
        {
            var result = await searchIndexClient.CreateOrUpdateIndexAsync(new SearchIndex(indexName, new FieldBuilder().Build(typeof(T))));
            Console.WriteLine($"Index create or update status: {result.GetRawResponse().Status}");
        }
        catch (Exception)
        {
            Console.WriteLine($"Unable to create or update index {indexName}");
        }
    }
}
