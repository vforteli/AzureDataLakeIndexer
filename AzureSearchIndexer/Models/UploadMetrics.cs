namespace AzureSearchIndexer;

public record UploadMetrics(int ProcessedCount, int FailedCoumt, int CreatedCount, int ModifiedCount, int FailedTooLargeCount);
