using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Exceptions;
using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Services;

/// <summary>
/// Implements Bulk API v2 ingest operations (insert, update, upsert, delete,
/// hardDelete) for both CSV and JSON payloads.
/// </summary>
public class BulkApiService : IBulkApiService
{
    private readonly SalesforceConfig _config;
    private readonly ISalesforceAuthService _auth;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BulkApiService> _logger;

    public BulkApiService(
        SalesforceConfig config,
        ISalesforceAuthService auth,
        HttpClient httpClient,
        ILogger<BulkApiService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public Task<BulkJobResult> ExecuteCsvJobAsync(
        string sfObject,
        OperationType operation,
        string csvData,
        string? externalIdField = null,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(sfObject, operation, externalIdField);
        if (string.IsNullOrWhiteSpace(csvData))
            throw new ArgumentException("csvData must not be empty.", nameof(csvData));

        return ExecuteIngestJobAsync(sfObject, operation, ContentType.Csv, csvData, externalIdField, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<BulkJobResult> ExecuteJsonJobAsync(
        string sfObject,
        OperationType operation,
        string jsonData,
        string? externalIdField = null,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(sfObject, operation, externalIdField);
        if (string.IsNullOrWhiteSpace(jsonData))
            throw new ArgumentException("jsonData must not be empty.", nameof(jsonData));

        return ExecuteIngestJobAsync(sfObject, operation, ContentType.Json, jsonData, externalIdField, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BulkJobInfo> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        return await FetchJobInfoAsync(instanceUrl, jobId, token, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AbortJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        await PatchJobStateAsync(instanceUrl, jobId, "Aborted", token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Aborted bulk ingest job {JobId}.", jobId);
    }

    // -------------------------------------------------------------------------
    // Core ingest workflow
    // -------------------------------------------------------------------------

    private async Task<BulkJobResult> ExecuteIngestJobAsync(
        string sfObject,
        OperationType operation,
        ContentType contentType,
        string data,
        string? externalIdField,
        CancellationToken cancellationToken)
    {
        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        var baseUrl = BuildBaseUrl(instanceUrl);

        // 1. Create the job
        var jobInfo = await CreateIngestJobAsync(baseUrl, sfObject, operation, contentType, externalIdField, token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created ingest job {JobId} for {Object}.{Operation}.", jobInfo.Id, sfObject, operation);

        // 2. Upload data
        await UploadJobDataAsync(baseUrl, jobInfo.Id, contentType, data, token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Uploaded data for job {JobId}.", jobInfo.Id);

        // 3. Close (signal upload complete)
        await PatchJobStateAsync(baseUrl, jobInfo.Id, "UploadComplete", token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Closed job {JobId}; waiting for processing.", jobInfo.Id);

        // 4. Poll for completion
        jobInfo = await PollJobUntilCompleteAsync(baseUrl, jobInfo.Id, token, cancellationToken).ConfigureAwait(false);

        if (jobInfo.State == JobState.Failed)
            throw new BulkJobException(jobInfo.Id, jobInfo.State,
                $"Bulk job {jobInfo.Id} failed: {jobInfo.ErrorMessage}");

        if (jobInfo.State == JobState.Aborted)
            throw new BulkJobException(jobInfo.Id, jobInfo.State,
                $"Bulk job {jobInfo.Id} was aborted.");

        // 5. Retrieve results
        return await CollectResultsAsync(baseUrl, jobInfo, token, cancellationToken).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // HTTP helpers
    // -------------------------------------------------------------------------

    private async Task<BulkJobInfo> CreateIngestJobAsync(
        string baseUrl,
        string sfObject,
        OperationType operation,
        ContentType contentType,
        string? externalIdField,
        string token,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["object"] = sfObject,
            ["operation"] = operation.ToString().ToLowerInvariant(),
            ["contentType"] = contentType == ContentType.Csv ? "CSV" : "JSON"
        };

        if (operation == OperationType.Upsert && !string.IsNullOrWhiteSpace(externalIdField))
            body["externalIdFieldName"] = externalIdField;

        var response = await SendJsonAsync(HttpMethod.Post, $"{baseUrl}/jobs/ingest", body, token, ct).ConfigureAwait(false);
        return await DeserializeAsync<BulkJobInfo>(response, ct).ConfigureAwait(false);
    }

    private async Task UploadJobDataAsync(
        string baseUrl,
        string jobId,
        ContentType contentType,
        string data,
        string token,
        CancellationToken ct)
    {
        var mediaType = contentType == ContentType.Csv ? "text/csv" : "application/json";
        var content = new StringContent(data, Encoding.UTF8, mediaType);
        var url = $"{baseUrl}/jobs/ingest/{jobId}/batches";

        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"uploading data for job {jobId}", ct).ConfigureAwait(false);
    }

    private async Task PatchJobStateAsync(
        string baseUrl,
        string jobId,
        string state,
        string token,
        CancellationToken ct)
    {
        var body = new Dictionary<string, string> { ["state"] = state };
        var url = $"{baseUrl}/jobs/ingest/{jobId}";
        var response = await SendJsonAsync(new HttpMethod("PATCH"), url, body, token, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"patching job {jobId} state to '{state}'", ct).ConfigureAwait(false);
    }

    private async Task<BulkJobInfo> FetchJobInfoAsync(
        string baseUrl,
        string jobId,
        string token,
        CancellationToken ct)
    {
        var url = $"{baseUrl}/jobs/ingest/{jobId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"fetching job status for {jobId}", ct).ConfigureAwait(false);
        return await DeserializeAsync<BulkJobInfo>(response, ct).ConfigureAwait(false);
    }

    private async Task<BulkJobInfo> PollJobUntilCompleteAsync(
        string baseUrl,
        string jobId,
        string token,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _config.MaxPollAttempts; attempt++)
        {
            await Task.Delay(_config.PollIntervalMs, ct).ConfigureAwait(false);

            var info = await FetchJobInfoAsync(baseUrl, jobId, token, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Job {JobId} status: {State} | Processed: {Processed} | Failed: {Failed} (poll {Attempt}/{Max})",
                jobId, info.State, info.NumberRecordsProcessed, info.NumberRecordsFailed,
                attempt, _config.MaxPollAttempts);

            if (info.State is JobState.JobComplete or JobState.Failed or JobState.Aborted)
                return info;
        }

        throw new BulkJobException(jobId, JobState.InProgress,
            $"Bulk job {jobId} did not complete within {_config.MaxPollAttempts} poll attempts " +
            $"(interval {_config.PollIntervalMs} ms).");
    }

    private async Task<BulkJobResult> CollectResultsAsync(
        string baseUrl,
        BulkJobInfo jobInfo,
        string token,
        CancellationToken ct)
    {
        var successCsv = await DownloadResultCsvAsync(baseUrl, jobInfo.Id, "successfulResults", token, ct).ConfigureAwait(false);
        var failedCsv = await DownloadResultCsvAsync(baseUrl, jobInfo.Id, "failedResults", token, ct).ConfigureAwait(false);
        var unprocessedCsv = await DownloadResultCsvAsync(baseUrl, jobInfo.Id, "unprocessedrecords", token, ct).ConfigureAwait(false);

        long unprocessedCount = CountCsvRows(unprocessedCsv);

        _logger.LogInformation(
            "Job {JobId} complete. Processed: {Processed}, Failed: {Failed}, Unprocessed: {Unprocessed}",
            jobInfo.Id, jobInfo.NumberRecordsProcessed, jobInfo.NumberRecordsFailed, unprocessedCount);

        return new BulkJobResult
        {
            JobInfo = jobInfo,
            SuccessfulRecordsCsv = successCsv,
            FailedRecordsCsv = failedCsv,
            UnprocessedRecordsCsv = unprocessedCsv,
            UnprocessedCount = unprocessedCount
        };
    }

    private async Task<string> DownloadResultCsvAsync(
        string baseUrl,
        string jobId,
        string resultType,
        string token,
        CancellationToken ct)
    {
        var url = $"{baseUrl}/jobs/ingest/{jobId}/{resultType}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        // 404 simply means there were no records of that type.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return string.Empty;

        await EnsureSuccessAsync(response, $"downloading {resultType} for job {jobId}", ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private async Task<(string token, string instanceUrl)> GetAuthAsync(CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct).ConfigureAwait(false);
        var instanceUrl = await _auth.GetInstanceUrlAsync(ct).ConfigureAwait(false);
        return (token, instanceUrl);
    }

    private string BuildBaseUrl(string instanceUrl) =>
        $"{instanceUrl.TrimEnd('/')}/services/data/{_config.ApiVersion}";

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string url,
        object body,
        string token,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions.Default);
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string context,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new SalesforceException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} while {context}: {body}");
        }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(body, JsonOptions.Default)
               ?? throw new SalesforceException($"Null response body deserializing {typeof(T).Name}.");
    }

    private static long CountCsvRows(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return 0;
        // Subtract 1 for the header row
        long count = -1;
        foreach (var ch in csv)
        {
            if (ch == '\n') count++;
        }
        return Math.Max(count, 0);
    }

    private static void ValidateArguments(string sfObject, OperationType operation, string? externalIdField)
    {
        if (string.IsNullOrWhiteSpace(sfObject))
            throw new ArgumentException("sfObject must not be empty.", nameof(sfObject));
        if (operation == OperationType.Upsert && string.IsNullOrWhiteSpace(externalIdField))
            throw new ArgumentException("externalIdField is required for Upsert operations.", nameof(externalIdField));
    }
}
