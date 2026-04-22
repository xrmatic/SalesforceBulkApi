using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Represents the status and metadata of a Bulk API v2 query job.
/// </summary>
public class QueryJobInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public JobState State { get; set; }

    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("numberRecordsProcessed")]
    public long NumberRecordsProcessed { get; set; }

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
