using Azure.Search.Documents;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AzureSearchIndexerTests;

public class DataLakeIndexerTests
{


    [Test]
    public async Task TestDataLakeIndexer_ListPathsFail_Terminates()
    {
        Func<PathIndexModel, FileDownloadInfo, Task<string?>> somefunc = async (path, file) =>
        {
            return await Task.FromResult("42");
        };

        var searchClient = Substitute.For<SearchClient>();
        var datalakeServiceClient = Substitute.For<DataLakeServiceClient>();
        var paths = Substitute.For<IAsyncEnumerable<PathIndexModel>>();
        paths.GetAsyncEnumerator().Throws(new Exception("nope..."));


        var dataLakeIndexer = new DataLakeIndexer(searchClient, NullLogger<DataLakeIndexer>.Instance, new DatalakeIndexerOptions { });

        var result = await dataLakeIndexer.RunDocumentIndexerOnPathsAsync(datalakeServiceClient, paths, somefunc, default);

        // the actual result is not really relevant... the purpose of this test is to ensure the indexing terminates on exceptions...
        Assert.That(result, Is.Not.Null);
    }
}