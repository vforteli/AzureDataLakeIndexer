namespace AzureSearchIndexer;

public record UploadMetrics(int ProcessedCount, int FailedCount, int CreatedCount, int ModifiedCount, int FailedTooLargeCount);
