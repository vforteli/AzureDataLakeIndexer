namespace AzureSearchIndexer;

public record ReadDocumentsMetrics
{
    required public long ReadCount { get; init; }
    required public long ReadFailedCount { get; init; }
}
