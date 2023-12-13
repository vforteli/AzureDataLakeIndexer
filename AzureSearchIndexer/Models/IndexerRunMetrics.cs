namespace AzureSearchIndexer;

public record IndexerRunMetrics
{
    required public long ReadCount { get; init; }
    required public long ReadFailedCount { get; init; }
    required public long ProcessedCount { get; init; }
    required public long UploadFailedCount { get; init; }
    required public long UploadFailedTooLargeCount { get; init; }
    required public long UploadCreatedCount { get; init; }
    required public long UploadModifiedCount { get; init; }
}
