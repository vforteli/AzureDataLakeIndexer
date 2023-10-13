using System.Text;
using Azure.Search.Documents.Indexes;

namespace AzureSearchIndexer;

/// <summary>
/// PathIndexModel is used to store blobcreated and blobdeleted event data
/// </summary>
public record PathIndexModel
{
    // not sure if this is such a great idea... although this index should be pretty self sufficient and not tinkered with from the outside
    [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)]
    public string key => Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("/", filesystem, path)));

    [SearchableField(IsFilterable = true, AnalyzerName = "keyword")]
    required public string path { get; init; } = "";

    [SimpleField(IsFilterable = true)]
    required public string filesystem { get; init; } = "";

    [SimpleField(IsFilterable = true, IsSortable = true)]
    required public DateTimeOffset lastModified { get; init; }
}
