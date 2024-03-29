using System.Threading.Channels;
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

        var documents = Channel.CreateUnbounded<PathIndexModel>();
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        documents.Writer.Complete();

        var uploadTask = uploader.UploadBatchesAsync(documents.Reader, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            searchClient.ReceivedWithAnyArgs(2).MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>());
            Assert.That(documents.Reader, Is.Empty);
            Assert.That(uploadTask.Result.ProcessedCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task TestBatchingUploaderEvenBatches()
    {
        var searchClient = Substitute.For<SearchClient>();
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2);

        var documents = Channel.CreateUnbounded<PathIndexModel>();
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        documents.Writer.Complete();

        var uploadTask = uploader.UploadBatchesAsync(documents.Reader, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            searchClient.ReceivedWithAnyArgs(2).MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>());
            Assert.That(documents.Reader, Is.Empty);
            Assert.That(uploadTask.Result.ProcessedCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task TestBatchingUploaderLessThanBatch()
    {
        var searchClient = Substitute.For<SearchClient>();
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2);

        var documents = Channel.CreateUnbounded<PathIndexModel>();
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        documents.Writer.Complete();

        var uploadTask = uploader.UploadBatchesAsync(documents.Reader, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            searchClient.ReceivedWithAnyArgs(1).MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>());
            Assert.That(documents.Reader, Is.Empty);
            Assert.That(uploadTask.Result.ProcessedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TestBatchingUploaderBatchSizeBytes()
    {
        var searchClient = Substitute.For<SearchClient>();
        var fakeSize = await Utils.GetJsonLengthAsync(Substitute.For<PathIndexModel>()).ConfigureAwait(false) + 1;
        var uploader = new BatchingUploader(NullLogger<BatchingUploader>.Instance, 2, 2, fakeSize);

        var documents = Channel.CreateUnbounded<PathIndexModel>();
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        await documents.Writer.WriteAsync(Substitute.For<PathIndexModel>());
        documents.Writer.Complete();

        var uploadTask = uploader.UploadBatchesAsync(documents.Reader, searchClient);
        await uploadTask;

        Assert.Multiple(() =>
        {
            searchClient.ReceivedWithAnyArgs(4).MergeOrUploadDocumentsAsync(Arg.Any<IEnumerable<PathIndexModel>>());
            Assert.That(documents.Reader, Is.Empty);
            Assert.That(uploadTask.Result.ProcessedCount, Is.EqualTo(4));
        });
    }
}