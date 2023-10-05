using System.Text.Json;
using Azure.Storage.Files.DataLake;

namespace SearchIndexerTest;


public static class DataLakeWriter
{
    const string Text = "this contains some text... yaaaay thanks for the fish etc, all work and no play makes something.... ";


    public static async Task WriteStuff(DataLakeFileSystemClient dataLakeFileSystemClient)
    {
        var toplevel = Enumerable.Range(0, 100).ToList();
        var objectRange = Enumerable.Range(0, 1000).ToList();
        var filesRange = Enumerable.Range(0, 10).ToList();

        var moreText = string.Concat(Enumerable.Repeat(Text, 1));
        var payload = new TestIndexModel { booleanvalue = true, numbervalue = 42, stringvalue = moreText };

        int count = 0;
        var paths = toplevel.SelectMany(partition => objectRange.SelectMany(o => filesRange.Select(f => $"partition_{partition}/customer_{o}/document_{f}.json"))).ToList().OrderBy(o => Guid.NewGuid()).ToList();


        using var foo = new Timer((o) => { Console.WriteLine($"uploaded {count} files and directories..."); }, null, 1000, 1000);
        await Parallel.ForEachAsync(paths, new ParallelOptions { MaxDegreeOfParallelism = 300 }, async (path, token) =>
        {
            var fileClient = dataLakeFileSystemClient.GetFileClient(path);

            using var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, payload); // not exactly efficient, but whatever
            memoryStream.Position = 0;
            await fileClient.UploadAsync(memoryStream, overwrite: true);
            await memoryStream.FlushAsync();

            Interlocked.Increment(ref count);
        });

        Console.WriteLine($"uploaded {count} files and directories...");
    }
}
