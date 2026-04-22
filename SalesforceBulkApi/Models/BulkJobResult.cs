namespace SalesforceBulkApi.Models;

/// <summary>
/// The complete result of a Bulk API v2 ingest job, including counts and
/// CSV records for successful, failed, and unprocessed rows.
/// </summary>
public class BulkJobResult
{
    /// <summary>Job status snapshot at completion.</summary>
    public BulkJobInfo JobInfo { get; set; } = new();

    /// <summary>
    /// CSV text of successfully processed records. Each row contains the
    /// original fields plus <c>sf__Id</c> and <c>sf__Created</c> columns
    /// added by Salesforce.
    /// </summary>
    public string SuccessfulRecordsCsv { get; set; } = string.Empty;

    /// <summary>
    /// CSV text of failed records. Each row contains <c>sf__Id</c>,
    /// <c>sf__Error</c> and the original fields.
    /// </summary>
    public string FailedRecordsCsv { get; set; } = string.Empty;

    /// <summary>
    /// CSV text of records that were not processed (e.g. because the job
    /// was aborted before they were reached).
    /// </summary>
    public string UnprocessedRecordsCsv { get; set; } = string.Empty;

    /// <summary>Total records in the submitted data.</summary>
    public long TotalRecords => JobInfo.NumberRecordsProcessed + UnprocessedCount;

    /// <summary>Number of successfully processed records.</summary>
    public long SuccessCount => JobInfo.NumberRecordsProcessed - JobInfo.NumberRecordsFailed;

    /// <summary>Number of failed records.</summary>
    public long FailedCount => JobInfo.NumberRecordsFailed;

    /// <summary>Number of unprocessed records.</summary>
    public long UnprocessedCount { get; set; }

    /// <summary>Whether the job completed without any failures.</summary>
    public bool IsFullySuccessful => JobInfo.State == JobState.JobComplete && FailedCount == 0 && UnprocessedCount == 0;
}
