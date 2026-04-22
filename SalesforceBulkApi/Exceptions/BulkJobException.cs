using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Exceptions;

/// <summary>
/// Thrown when a Bulk API v2 job fails or is aborted.
/// </summary>
public class BulkJobException : SalesforceException
{
    /// <summary>The Salesforce Bulk job ID.</summary>
    public string JobId { get; }

    /// <summary>The final state of the job at the time of failure.</summary>
    public JobState JobState { get; }

    public BulkJobException(string jobId, JobState jobState, string message)
        : base(message)
    {
        JobId = jobId;
        JobState = jobState;
    }

    public BulkJobException(string jobId, JobState jobState, string message, Exception inner)
        : base(message, inner)
    {
        JobId = jobId;
        JobState = jobState;
    }
}
