using Azure.Search.Documents.Indexes;

namespace AzureSearchIndexer;



public abstract record BaseIndexModel
{
    [SimpleField(IsKey = true)]
    public string pathbase64 { get; init; } = "";
}

public record TestIndexModel : BaseIndexModel
{
    [SearchableField]
    required public string stringvalue { get; init; } = "";

    [SimpleField(IsFacetable = true, IsFilterable = true)]
    required public int numbervalue { get; init; }

    [SimpleField(IsFacetable = true, IsFilterable = true)]
    required public bool booleanvalue { get; init; }
}


public record SomeOtherIndexModel : TestIndexModel
{
    [SimpleField]
    required public string eTag { get; init; } = "";

    [SimpleField]
    required public string pathUrlEncoded { get; init; } = "";

    [SimpleField(IsFilterable = true, IsSortable = true)]
    required public DateTimeOffset lastModified { get; init; }
}
