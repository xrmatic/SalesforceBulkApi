using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Services;

/// <summary>
/// Creates, monitors, and retrieves results for Bulk API v2 ingest jobs
/// (insert, update, upsert, delete, hardDelete).
/// </summary>
public interface IBulkApiService
{
    /// <summary>
    /// Creates an ingest job, uploads CSV data, closes the job, polls until
    /// complete and returns the full result set.
    /// </summary>
    /// <param name="sfObject">API name of the Salesforce object (e.g. "Account").</param>
    /// <param name="operation">DML operation to perform.</param>
    /// <param name="csvData">
    /// Well-formed CSV string. The first row must be a header row with API
    /// field names. Use <c>Id</c> for updates/deletes; add an external ID
    /// column for upserts.
    /// </param>
    /// <param name="externalIdField">
    /// Required for <see cref="OperationType.Upsert"/>; ignored otherwise.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BulkJobResult"/> with counts and result CSVs.</returns>
    Task<BulkJobResult> ExecuteCsvJobAsync(
        string sfObject,
        OperationType operation,
        string csvData,
        string? externalIdField = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an ingest job, uploads JSON data, closes the job, polls until
    /// complete and returns the full result set.
    /// </summary>
    /// <param name="sfObject">API name of the Salesforce object.</param>
    /// <param name="operation">DML operation to perform.</param>
    /// <param name="jsonData">
    /// JSON array of record objects. Each object's keys are API field names.
    /// </param>
    /// <param name="externalIdField">
    /// Required for <see cref="OperationType.Upsert"/>; ignored otherwise.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BulkJobResult"/> with counts and result CSVs.</returns>
    Task<BulkJobResult> ExecuteJsonJobAsync(
        string sfObject,
        OperationType operation,
        string jsonData,
        string? externalIdField = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current status of an ingest job.
    /// </summary>
    Task<BulkJobInfo> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts an open or in-progress ingest job.
    /// </summary>
    Task AbortJobAsync(string jobId, CancellationToken cancellationToken = default);
}
