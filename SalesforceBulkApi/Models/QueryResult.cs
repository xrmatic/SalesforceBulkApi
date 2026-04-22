using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Holds a single page of query results from the Bulk API v2 query endpoint.
/// </summary>
internal class QueryResultPage
{
    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// Opaque cursor to pass to the next request via the <c>locator</c>
    /// query-string parameter. Null or absent when there are no more pages.
    /// </summary>
    [JsonPropertyName("nextRecordsUrl")]
    public string? NextRecordsUrl { get; set; }

    /// <summary>
    /// The raw JSON array of record objects returned by Salesforce.
    /// Stored as a <see cref="System.Text.Json.JsonElement"/> so callers can
    /// merge pages without re-serialising.
    /// </summary>
    [JsonPropertyName("records")]
    public System.Text.Json.JsonElement Records { get; set; }
}

/// <summary>
/// The complete result of a Bulk API v2 query job.
/// </summary>
public class QueryResult
{
    /// <summary>Job status snapshot at completion.</summary>
    public QueryJobInfo JobInfo { get; set; } = new();

    /// <summary>
    /// All records as a JSON array string. Each element is a Salesforce
    /// record object with the fields requested in the SOQL query.
    /// </summary>
    public string RecordsJson { get; set; } = "[]";

    /// <summary>Total number of records retrieved.</summary>
    public long TotalRecords { get; set; }

    /// <summary>Number of pages fetched to assemble the full result set.</summary>
    public int PagesFetched { get; set; }
}
