using Azure.Search.Documents.Indexes;

namespace AzureSearchIndexer;

/// <summary>
/// PathIndexModel is used to store blobcreated and blobdeleted event data
/// </summary>
public record PathIndexModel
{
    // hmm.. maybe path as key? or something else? hash?
    [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)]
    public string key { get; init; } = "";

    [SimpleField(IsFilterable = true)]
    public string path { get; init; } = "";

    [SimpleField(IsFilterable = true)]
    public string filesystem { get; init; } = "";

    [SimpleField(IsFilterable = true, IsSortable = true)]
    public DateTimeOffset lastModified { get; init; }
}
