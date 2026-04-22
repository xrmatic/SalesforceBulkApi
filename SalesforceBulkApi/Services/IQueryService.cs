using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Services;

/// <summary>
/// Executes Bulk API v2 query jobs and returns paginated results as a single
/// complete JSON dataset.
/// </summary>
public interface IQueryService
{
    /// <summary>
    /// Creates a query job, waits for completion, paginates through all result
    /// pages and returns the full record set as a JSON array string.
    /// </summary>
    /// <param name="soqlQuery">
    /// A valid SOQL query string (e.g. <c>SELECT Id, Name FROM Account</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="QueryResult"/> containing the complete JSON array and
    /// metadata about the job and pagination.
    /// </returns>
    Task<QueryResult> ExecuteQueryAsync(
        string soqlQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current status of a query job.
    /// </summary>
    Task<QueryJobInfo> GetQueryJobStatusAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts an open or in-progress query job.
    /// </summary>
    Task AbortQueryJobAsync(string jobId, CancellationToken cancellationToken = default);
}
