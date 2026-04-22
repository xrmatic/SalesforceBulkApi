using System.Text.Json.Serialization;
using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Represents the status and metadata of a Bulk API v2 ingest job.
/// </summary>
public class BulkJobInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public OperationType Operation { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public JobState State { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("apiVersion")]
    public double ApiVersion { get; set; }

    [JsonPropertyName("externalIdFieldName")]
    public string? ExternalIdFieldName { get; set; }

    [JsonPropertyName("numberRecordsProcessed")]
    public long NumberRecordsProcessed { get; set; }

    [JsonPropertyName("numberRecordsFailed")]
    public long NumberRecordsFailed { get; set; }

    [JsonPropertyName("totalProcessingTime")]
    public long TotalProcessingTime { get; set; }

    [JsonPropertyName("apiActiveProcessingTime")]
    public long ApiActiveProcessingTime { get; set; }

    [JsonPropertyName("apexProcessingTime")]
    public long ApexProcessingTime { get; set; }

    [JsonPropertyName("createdDate")]
    public string? CreatedDate { get; set; }

    [JsonPropertyName("systemModstamp")]
    public string? SystemModstamp { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
