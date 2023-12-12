using System.Collections.Concurrent;
using Azure.Search.Documents;
using AzureSearchIndexer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AzureSearchIndexerTests;

public class BatchingUploaderTests
{
    [Test]
    public async Task TestBatchingUploader()
    {
        var searchClient = Substitute.For<SearchClient>();
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2);

        var documents = new BlockingCollection<PathIndexModel>
        {
            Substitute.For<PathIndexModel>(),
            Substitute.For<PathIndexModel>(),
            Substitute.For<PathIndexModel>(),
        };

        documents.CompleteAdding();

        var uploadTask = uploader.UploadBatchesAsync(documents, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            Assert.That(documents.Count, Is.EqualTo(0));
            Assert.That(uploadTask.Result.UploadCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task TestBatchingUploaderEvenBatches()
    {
        var searchClient = Substitute.For<SearchClient>();
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2);

        var documents = new BlockingCollection<PathIndexModel>
        {
            Substitute.For<PathIndexModel>(),
            Substitute.For<PathIndexModel>(),
            Substitute.For<PathIndexModel>(),
            Substitute.For<PathIndexModel>(),
        };

        documents.CompleteAdding();

        var uploadTask = uploader.UploadBatchesAsync(documents, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            Assert.That(documents.Count, Is.EqualTo(0));
            Assert.That(uploadTask.Result.UploadCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task TestBatchingUploaderLessThanBatch()
    {
        var searchClient = Substitute.For<SearchClient>();
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2);

        var documents = new BlockingCollection<PathIndexModel>
        {
            Substitute.For<PathIndexModel>(),
        };

        documents.CompleteAdding();

        var uploadTask = uploader.UploadBatchesAsync(documents, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            Assert.That(documents.Count, Is.EqualTo(0));
            Assert.That(uploadTask.Result.UploadCount, Is.EqualTo(1));
        });
    }
}