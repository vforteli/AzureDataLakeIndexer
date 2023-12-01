namespace AzureSearchIndexer;

public record IndexerRunMetrics
{
    required public long DocumentReadCount { get; init; }
    required public long DocumentReadFailedCount { get; init; }
    required public long DocumentUploadCount { get; init; }
    required public long DocumentUploadFailedCount { get; init; }
    required public long DocumentUploadCreatedCount { get; init; }
    required public long DocumentUploadModifiedCount { get; init; }
}
