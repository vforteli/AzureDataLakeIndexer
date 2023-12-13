namespace AzureSearchIndexer;

public record DatalakeIndexerOptions
{
    public int MaxReatThreads { get; init; } = 128;

    public int MaxUploadThreads { get; init; } = 4;

    public int DocumentBatchSize { get; init; } = 1000;

    public int MaxDocumentBatchSizeBytes { get; init; } = 63 * 1024 * 1024;

    public int MaxDocumentSizeBytes { get; init; } = 16 * 1024 * 1024;
}
