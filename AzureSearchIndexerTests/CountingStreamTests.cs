using System.Text.Json;
using AzureSearchIndexer;

namespace AzureSearchIndexerTests;

public class CountingStreamTests
{
    [Test]
    public async Task TestCountSmallObject()
    {
        var someObject = new PathIndexModel
        {
            fileLastModified = DateTimeOffset.MinValue,
            filesystem = "somefilesystem",
            lastModified = DateTimeOffset.MinValue,
            pathUrlEncoded = "notreally",
        };

        var length = await Utils.GetJsonLengthAsync(someObject);

        Assert.That(length, Is.EqualTo(195));
    }

    [Test]
    public async Task TestCountSmallObjectWithOptions()
    {
        var someObject = new PathIndexModel
        {
            fileLastModified = DateTimeOffset.MinValue,
            filesystem = "somefilesystem",
            lastModified = DateTimeOffset.MinValue,
            pathUrlEncoded = "notreally",
        };

        var length = await Utils.GetJsonLengthAsync(someObject, new JsonSerializerOptions { WriteIndented = true });

        Assert.That(length, Is.EqualTo(216));
    }

    [Test]
    public async Task TestCountSlightlyLargerObject()
    {
        var text = string.Join("", Enumerable.Repeat("a", 100000));
        var someObject = new PathIndexModel
        {
            fileLastModified = DateTimeOffset.MinValue,
            filesystem = text,
            lastModified = DateTimeOffset.MinValue,
            pathUrlEncoded = "notreally",
        };

        for (var i = 0; i < 100; i++)
        {
            var length = await Utils.GetJsonLengthAsync(someObject);
            Assert.That(length, Is.EqualTo(233497));
        }
    }
}