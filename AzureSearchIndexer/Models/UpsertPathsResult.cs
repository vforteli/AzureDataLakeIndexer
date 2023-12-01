namespace AzureSearchIndexer;

public record UpsertPathsResult
{
    required public long Created { get; init; }
    required public long Modified { get; init; }
    required public long Failed { get; init; }
}