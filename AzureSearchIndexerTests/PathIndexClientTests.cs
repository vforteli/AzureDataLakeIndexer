namespace AzureSearchIndexerTests;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

public class Tests
{
    [Test]
    public async Task TestEmptyPathsResult()
    {
        // this basically only tests that the method ends and doesnt go into an infinite loop...
        var fileSystemClient = Substitute.For<DataLakeFileSystemClient>();
        var searchClient = Substitute.For<SearchClient>();
        var pathIndexClient = new PathIndexClient(searchClient, NullLogger<PathIndexClient>.Instance);

        var result = await pathIndexClient.RebuildPathsIndexAsync(fileSystemClient, "somepath");

        Assert.That(result.Created, Is.EqualTo(0));
    }
}
