using SalesforceBulkApi.Models;
using SalesforceBulkApi.Services;

namespace SalesforceBulkApi;

/// <summary>
/// High-level CRUD client for Salesforce Bulk API v2. This is the primary
/// entry point for consumers of the library.
/// </summary>
/// <remarks>
/// Obtain an instance via <see cref="SalesforceBulkClientFactory"/> or the DI
/// extension <c>services.AddSalesforceBulkApi(config)</c>.
/// </remarks>
public class SalesforceBulkClient
{
    private readonly IBulkApiService _bulkApi;
    private readonly IQueryService _queryService;

    public SalesforceBulkClient(IBulkApiService bulkApi, IQueryService queryService)
    {
        _bulkApi = bulkApi ?? throw new ArgumentNullException(nameof(bulkApi));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
    }

    // =========================================================================
    // CREATE
    // =========================================================================

    /// <summary>
    /// Inserts records using CSV data. The first row must be a header with API
    /// field names. Returns a result with counts and result CSVs.
    /// </summary>
    public Task<BulkJobResult> CreateFromCsvAsync(
        string sfObject,
        string csvData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteCsvJobAsync(sfObject, OperationType.Insert, csvData, null, cancellationToken);

    /// <summary>
    /// Inserts records using a JSON array. Each element's keys are API field names.
    /// </summary>
    public Task<BulkJobResult> CreateFromJsonAsync(
        string sfObject,
        string jsonData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteJsonJobAsync(sfObject, OperationType.Insert, jsonData, null, cancellationToken);

    // =========================================================================
    // READ
    // =========================================================================

    /// <summary>
    /// Executes a SOQL query and returns the complete result set as a JSON
    /// array. Results are automatically paginated — all pages are fetched and
    /// merged before returning.
    /// </summary>
    /// <param name="soqlQuery">A valid SOQL query, e.g. SELECT Id, Name FROM Account.</param>
    public Task<QueryResult> ReadAsync(
        string soqlQuery,
        CancellationToken cancellationToken = default) =>
        _queryService.ExecuteQueryAsync(soqlQuery, cancellationToken);

    // =========================================================================
    // UPDATE
    // =========================================================================

    /// <summary>
    /// Updates existing records using CSV data. The CSV must include an <c>Id</c> column.
    /// </summary>
    public Task<BulkJobResult> UpdateFromCsvAsync(
        string sfObject,
        string csvData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteCsvJobAsync(sfObject, OperationType.Update, csvData, null, cancellationToken);

    /// <summary>
    /// Updates existing records using JSON data. Each object must include an <c>Id</c> field.
    /// </summary>
    public Task<BulkJobResult> UpdateFromJsonAsync(
        string sfObject,
        string jsonData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteJsonJobAsync(sfObject, OperationType.Update, jsonData, null, cancellationToken);

    // =========================================================================
    // UPSERT
    // =========================================================================

    /// <summary>
    /// Upserts records using CSV data. The CSV must include the external ID column.
    /// </summary>
    /// <param name="externalIdField">API name of the external ID field (e.g. "ExternalId__c").</param>
    public Task<BulkJobResult> UpsertFromCsvAsync(
        string sfObject,
        string csvData,
        string externalIdField,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteCsvJobAsync(sfObject, OperationType.Upsert, csvData, externalIdField, cancellationToken);

    /// <summary>
    /// Upserts records using JSON data. Each object must include the external ID field.
    /// </summary>
    public Task<BulkJobResult> UpsertFromJsonAsync(
        string sfObject,
        string jsonData,
        string externalIdField,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteJsonJobAsync(sfObject, OperationType.Upsert, jsonData, externalIdField, cancellationToken);

    // =========================================================================
    // DELETE
    // =========================================================================

    /// <summary>
    /// Deletes records (moves to recycle bin) using CSV data.
    /// The CSV must include only an <c>Id</c> column.
    /// </summary>
    public Task<BulkJobResult> DeleteFromCsvAsync(
        string sfObject,
        string csvData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteCsvJobAsync(sfObject, OperationType.Delete, csvData, null, cancellationToken);

    /// <summary>
    /// Deletes records (moves to recycle bin) using JSON data.
    /// Each object must include only an <c>Id</c> field.
    /// </summary>
    public Task<BulkJobResult> DeleteFromJsonAsync(
        string sfObject,
        string jsonData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteJsonJobAsync(sfObject, OperationType.Delete, jsonData, null, cancellationToken);

    /// <summary>
    /// Hard-deletes records (bypasses recycle bin) using CSV data.
    /// The CSV must include only an <c>Id</c> column.
    /// </summary>
    public Task<BulkJobResult> HardDeleteFromCsvAsync(
        string sfObject,
        string csvData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteCsvJobAsync(sfObject, OperationType.HardDelete, csvData, null, cancellationToken);

    /// <summary>
    /// Hard-deletes records (bypasses recycle bin) using JSON data.
    /// Each object must include only an <c>Id</c> field.
    /// </summary>
    public Task<BulkJobResult> HardDeleteFromJsonAsync(
        string sfObject,
        string jsonData,
        CancellationToken cancellationToken = default) =>
        _bulkApi.ExecuteJsonJobAsync(sfObject, OperationType.HardDelete, jsonData, null, cancellationToken);

    // =========================================================================
    // LOW-LEVEL ACCESS
    // =========================================================================

    /// <summary>Exposes the underlying ingest service for advanced use.</summary>
    public IBulkApiService BulkApi => _bulkApi;

    /// <summary>Exposes the underlying query service for advanced use.</summary>
    public IQueryService Query => _queryService;
}
