using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Represents the state of a Bulk API v2 job.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobState
{
    /// <summary>Job has been created and is accepting data.</summary>
    Open,

    /// <summary>Data upload is complete; the job is queued for processing.</summary>
    UploadComplete,

    /// <summary>The job is being processed.</summary>
    InProgress,

    /// <summary>The job has been aborted.</summary>
    Aborted,

    /// <summary>The job completed successfully or with partial failures.</summary>
    JobComplete,

    /// <summary>The job failed entirely.</summary>
    Failed
}
