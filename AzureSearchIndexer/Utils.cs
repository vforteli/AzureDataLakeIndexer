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
}
