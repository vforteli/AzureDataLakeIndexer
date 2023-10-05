using System.Text.Json.Serialization;

namespace DatalakePathIndexerFunc;

public record BlobEvent
{
    [JsonPropertyName("topic")]
    required public string Topic { get; init; }

    [JsonPropertyName("subject")]
    required public string Subject { get; init; }

    [JsonPropertyName("eventType")]
    required public string EventType { get; init; }

    [JsonPropertyName("id")]
    required public string Id { get; init; }

    [JsonPropertyName("data")]
    required public BlobEventData Data { get; init; }

    [JsonPropertyName("dataVersion")]
    required public string DataVersion { get; init; }

    [JsonPropertyName("metadataVersion")]
    required public string MetadataVersion { get; init; }

    [JsonPropertyName("eventTime")]
    required public DateTime EventTime { get; init; }
}

public record BlobEventData
{
    [JsonPropertyName("api")]
    required public string Api { get; init; }

    [JsonPropertyName("clientRequestId")]
    required public string ClientRequestId { get; init; }

    [JsonPropertyName("requestId")]
    required public string RequestId { get; init; }

    [JsonPropertyName("eTag")]
    required public string ETag { get; init; }

    [JsonPropertyName("contentType")]
    required public string ContentType { get; init; }

    [JsonPropertyName("contentLength")]
    required public long ContentLength { get; init; }

    [JsonPropertyName("contentOffset")]
    required public long ContentOffset { get; init; }

    [JsonPropertyName("blobType")]
    required public string BlobType { get; init; }

    [JsonPropertyName("blobProperties")]
    required public List<BlobProperty> BlobProperties { get; init; }

    [JsonPropertyName("blobUrl")]
    required public string BlobUrl { get; init; }

    [JsonPropertyName("url")]
    required public string Url { get; init; }

    [JsonPropertyName("sequencer")]
    required public string Sequencer { get; init; }

    [JsonPropertyName("identity")]
    required public string Identity { get; init; }

    [JsonPropertyName("storageDiagnostics")]
    required public StorageDiagnostics StorageDiagnostics { get; init; }
}

public record BlobProperty
{
    [JsonPropertyName("acl")]
    required public List<Acl> Acl { get; init; }
}

public record Acl
{
    [JsonPropertyName("access")]
    required public string Access { get; init; }

    [JsonPropertyName("permission")]
    required public string Permission { get; init; }

    [JsonPropertyName("owner")]
    required public string Owner { get; init; }

    [JsonPropertyName("group")]
    required public string Group { get; init; }
}

public record StorageDiagnostics
{
    [JsonPropertyName("batchId")]
    required public string BatchId { get; init; }
}
