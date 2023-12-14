using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AzureSearchIndexerTests;

public class Tests
{
    [Test]
    public async Task TestEmptyPathsResult()
    {
        // this basically only tests that the method ends and doesnt go into an infinite loop...
        var searchClient = Substitute.For<SearchClient>();

        var paths = new List<PathItem>().ToAsyncEnumerable();
        var result = await new PathIndexClient(searchClient, NullLogger<PathIndexClient>.Instance).RebuildPathsIndexAsync(paths, "somepath");

        Assert.That(result.Created, Is.EqualTo(0));
    }


    [Test]
    public async Task TestPathIndexRebuild()
    {
        var response = Substitute.For<Response<IndexDocumentsResult>>();
        response.Value.Returns(SearchModelFactory.IndexDocumentsResult(new List<IndexingResult>()));

        var searchClient = Substitute.For<SearchClient>();
        searchClient.MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>()).Returns(response);

        var paths = Enumerable.Range(0, 1000)
            .Select(o => DataLakeModelFactory.PathItem("somepath", false, default, default, default, default, default, default, default, default, default))
            .ToAsyncEnumerable();

        var actual = await new PathIndexClient(searchClient, NullLogger<PathIndexClient>.Instance).RebuildPathsIndexAsync(paths, "somepath");

        Assert.Multiple(() =>
        {
            Assert.That(actual.Created, Is.EqualTo(0));
            Assert.That(searchClient.ReceivedCalls().Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TestPathIndexRebuildMultipleBatches()
    {
        var response = Substitute.For<Response<IndexDocumentsResult>>();
        response.Value.Returns(SearchModelFactory.IndexDocumentsResult(new List<IndexingResult>()));

        var searchClient = Substitute.For<SearchClient>();
        searchClient.MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>()).Returns(response);

        var paths = Enumerable.Range(0, 1001)
            .Select(o => DataLakeModelFactory.PathItem("somepath", false, default, default, default, default, default, default, default, default, default))
            .ToAsyncEnumerable();

        var actual = await new PathIndexClient(searchClient, NullLogger<PathIndexClient>.Instance).RebuildPathsIndexAsync(paths, "somepath");

        Assert.Multiple(() =>
        {
            Assert.That(actual.Created, Is.EqualTo(0));
            Assert.That(searchClient.ReceivedCalls().Count, Is.EqualTo(2));
        });
    }
}
