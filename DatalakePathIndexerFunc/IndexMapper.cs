using System.Text.Json;
using System.Web;
using Azure.Storage.Files.DataLake.Models;
using AzureSearchIndexer;

namespace DatalakePathIndexerFunc;

public static class IndexMapper
{
    public static async Task<SomeOtherIndexModel?> MapSomethingToSomethingElseAsync(PathIndexModel path, FileDownloadInfo file)
    {
        var document = await JsonSerializer.DeserializeAsync<TestIndexModel>(file.Content).ConfigureAwait(false);

        return document != null
            ? new SomeOtherIndexModel
            {
                booleanvalue = document.booleanvalue,
                numbervalue = document.numbervalue,
                pathbase64 = path.key,
                stringvalue = document.stringvalue,
                eTag = file.Properties.ETag.ToString(),
                pathUrlEncoded = HttpUtility.UrlEncode(path.path),
                lastModified = path.lastModified,
            }
            : null;
    }
}

