namespace AzureSearchIndexer;

public record ListPathsOptions
{
    /// <summary>
    /// OData filter for eg running indexer with partitions by filtering to certain paths etc
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// Set to filter paths where last modified is greater or equal to value
    /// Should usually be set to the time of the last successful indexer run
    /// </summary>
    public DateTimeOffset? FromLastModified { get; init; }
}
