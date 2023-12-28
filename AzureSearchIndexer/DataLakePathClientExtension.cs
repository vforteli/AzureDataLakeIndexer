using Azure.Storage.Files.DataLake;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Azure.Storage.Files.DataLake.Models;
using System.Threading.Channels;

namespace DataLakeFileSystemClientExtension;

/// <summary>
/// Extension method for listing paths using many threads
/// </summary>
public static class DataLakeFileSystemClientExtension
{
    /// <summary>
    /// List paths recursively using multiple threads
    /// Fills provided blocking collection with PathItems
    /// </summary>
    /// <param name="dataLakeFileSystemClient">Authenticated filesystem client where the search should start</param>
    /// <param name="searchPath">Directory where recursive listing should start</param>
    /// <param name="paths">Channel where paths will be written</param>
    /// <param name="maxThreads">Max degrees of parallelism. Typically use something like 256</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task which completes when all items have been added to the blocking collection</returns>
    public static Task ListPathsParallelAsync(this DataLakeFileSystemClient dataLakeFileSystemClient, string searchPath, ChannelWriter<PathItem> paths, int maxThreads = 256, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        var filesCount = 0;
        var directoryPaths = Channel.CreateBounded<string>(int.MaxValue);
        var tasks = new ConcurrentDictionary<Guid, Task>();

        using var semaphore = new SemaphoreSlim(maxThreads, maxThreads);

        await directoryPaths.Writer.WriteAsync(searchPath).ConfigureAwait(false);

        try
        {
            while (await directoryPaths.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (directoryPaths.Reader.TryRead(out var directoryPath))
                {
                    var taskId = Guid.NewGuid();
                    tasks.TryAdd(taskId, Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var childPath in dataLakeFileSystemClient.GetPathsAsync(directoryPath, recursive: false, cancellationToken: cancellationToken).ConfigureAwait(false))
                            {
                                await paths.WriteAsync(childPath).ConfigureAwait(false);

                                if (!childPath.IsDirectory ?? false)
                                {
                                    var currentCount = Interlocked.Increment(ref filesCount);
                                }
                                else
                                {
                                    await directoryPaths.Writer.WriteAsync(childPath.Name).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (TaskCanceledException) { }
                        finally
                        {
                            tasks.TryRemove(taskId, out _);
                            semaphore.Release();

                            if (tasks.IsEmpty && directoryPaths.Reader.Count == 0)
                            {
                                directoryPaths.Writer.Complete();
                            }
                        }
                    }, cancellationToken));
                }
            }
        }
        catch (TaskCanceledException) { }
        finally
        {
            paths.Complete();
        }
    });


    /// <summary>
    /// List paths recursively using multiple threads
    /// </summary>
    /// <param name="dataLakeFileSystemClient"></param>
    /// <param name="searchPath"></param>
    /// <param name="maxThreads"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<PathItem> ListPathsParallelAsync(this DataLakeFileSystemClient dataLakeFileSystemClient, string searchPath, int maxThreads = 256, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var paths = Channel.CreateBounded<PathItem>(int.MaxValue);
        var task = dataLakeFileSystemClient.ListPathsParallelAsync(searchPath, paths, maxThreads, cancellationToken);

        await foreach (var path in paths.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return path;
        }

        await task;
    }
}
