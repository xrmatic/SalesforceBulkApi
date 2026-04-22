using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Exceptions;
using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Services;

/// <summary>
/// Implements Bulk API v2 query jobs and assembles all paginated result pages
/// into a single JSON array.
/// </summary>
public class QueryService : IQueryService
{
    private readonly SalesforceConfig _config;
    private readonly ISalesforceAuthService _auth;
    private readonly HttpClient _httpClient;
    private readonly ILogger<QueryService> _logger;

    public QueryService(
        SalesforceConfig config,
        ISalesforceAuthService auth,
        HttpClient httpClient,
        ILogger<QueryService> logger)
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
    public async Task<QueryResult> ExecuteQueryAsync(
        string soqlQuery,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(soqlQuery))
            throw new ArgumentException("soqlQuery must not be empty.", nameof(soqlQuery));

        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        var baseUrl = BuildBaseUrl(instanceUrl);

        // 1. Create query job
        var jobInfo = await CreateQueryJobAsync(baseUrl, soqlQuery, token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created query job {JobId}.", jobInfo.Id);

        // 2. Poll until complete
        jobInfo = await PollQueryJobAsync(baseUrl, jobInfo.Id, token, cancellationToken).ConfigureAwait(false);

        if (jobInfo.State == JobState.Failed)
            throw new BulkJobException(jobInfo.Id, jobInfo.State,
                $"Query job {jobInfo.Id} failed: {jobInfo.ErrorMessage}");

        if (jobInfo.State == JobState.Aborted)
            throw new BulkJobException(jobInfo.Id, jobInfo.State,
                $"Query job {jobInfo.Id} was aborted.");

        // 3. Paginate through all results and collect records
        var (recordsJson, totalRecords, pagesFetched) =
            await CollectAllQueryResultsAsync(baseUrl, jobInfo.Id, token, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Query job {JobId} complete. Total records: {Total}, Pages fetched: {Pages}",
            jobInfo.Id, totalRecords, pagesFetched);

        return new QueryResult
        {
            JobInfo = jobInfo,
            RecordsJson = recordsJson,
            TotalRecords = totalRecords,
            PagesFetched = pagesFetched
        };
    }

    /// <inheritdoc/>
    public async Task<QueryJobInfo> GetQueryJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        var baseUrl = BuildBaseUrl(instanceUrl);
        return await FetchQueryJobInfoAsync(baseUrl, jobId, token, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AbortQueryJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var (token, instanceUrl) = await GetAuthAsync(cancellationToken).ConfigureAwait(false);
        var baseUrl = BuildBaseUrl(instanceUrl);
        await PatchQueryJobStateAsync(baseUrl, jobId, "Aborted", token, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Aborted query job {JobId}.", jobId);
    }

    // -------------------------------------------------------------------------
    // HTTP helpers
    // -------------------------------------------------------------------------

    private async Task<QueryJobInfo> CreateQueryJobAsync(
        string baseUrl,
        string soqlQuery,
        string token,
        CancellationToken ct)
    {
        var body = new { operation = "query", query = soqlQuery };
        var response = await SendJsonAsync(HttpMethod.Post, $"{baseUrl}/jobs/query", body, token, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "creating query job", ct).ConfigureAwait(false);
        return await DeserializeAsync<QueryJobInfo>(response, ct).ConfigureAwait(false);
    }

    private async Task<QueryJobInfo> FetchQueryJobInfoAsync(
        string baseUrl,
        string jobId,
        string token,
        CancellationToken ct)
    {
        var url = $"{baseUrl}/jobs/query/{jobId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"fetching query job status for {jobId}", ct).ConfigureAwait(false);
        return await DeserializeAsync<QueryJobInfo>(response, ct).ConfigureAwait(false);
    }

    private async Task<QueryJobInfo> PollQueryJobAsync(
        string baseUrl,
        string jobId,
        string token,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _config.MaxPollAttempts; attempt++)
        {
            await Task.Delay(_config.PollIntervalMs, ct).ConfigureAwait(false);

            var info = await FetchQueryJobInfoAsync(baseUrl, jobId, token, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Query job {JobId} status: {State} | Processed: {Processed} (poll {Attempt}/{Max})",
                jobId, info.State, info.NumberRecordsProcessed, attempt, _config.MaxPollAttempts);

            if (info.State is JobState.JobComplete or JobState.Failed or JobState.Aborted)
                return info;
        }

        throw new BulkJobException(jobId, JobState.InProgress,
            $"Query job {jobId} did not complete within {_config.MaxPollAttempts} poll attempts " +
            $"(interval {_config.PollIntervalMs} ms).");
    }

    private async Task<(string recordsJson, long totalRecords, int pagesFetched)> CollectAllQueryResultsAsync(
        string baseUrl,
        string jobId,
        string token,
        CancellationToken ct)
    {
        var allRecords = new List<JsonElement>();
        long totalRecords = 0;
        int pagesFetched = 0;
        string? locator = null;

        do
        {
            var url = BuildResultsUrl(baseUrl, jobId, locator);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            // 204 No Content means there are no results
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                break;

            await EnsureSuccessAsync(response, $"fetching query results page {pagesFetched + 1} for job {jobId}", ct).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var page = JsonSerializer.Deserialize<QueryResultPage>(body, JsonOptions.Default);

            if (page is null) break;

            pagesFetched++;
            totalRecords += page.TotalSize;

            // Collect individual record elements
            if (page.Records.ValueKind == JsonValueKind.Array)
            {
                foreach (var record in page.Records.EnumerateArray())
                    allRecords.Add(record);
            }

            _logger.LogInformation(
                "Fetched page {Page}: {Count} records. Done: {Done}",
                pagesFetched, page.TotalSize, page.Done);

            // Extract the locator from the Sforce-Locator header for the next page
            locator = null;
            if (response.Headers.TryGetValues("Sforce-Locator", out var locatorValues))
            {
                var locatorValue = locatorValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(locatorValue) &&
                    !locatorValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    locator = locatorValue;
                }
            }

        } while (locator is not null);

        // Serialize the aggregated record list back to a JSON array
        var recordsJson = JsonSerializer.Serialize(allRecords, JsonOptions.Default);
        return (recordsJson, totalRecords, pagesFetched);
    }

    private async Task PatchQueryJobStateAsync(
        string baseUrl,
        string jobId,
        string state,
        string token,
        CancellationToken ct)
    {
        var body = new Dictionary<string, string> { ["state"] = state };
        var url = $"{baseUrl}/jobs/query/{jobId}";
        var response = await SendJsonAsync(new HttpMethod("PATCH"), url, body, token, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"patching query job {jobId} state to '{state}'", ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private string BuildResultsUrl(string baseUrl, string jobId, string? locator)
    {
        var url = $"{baseUrl}/jobs/query/{jobId}/results?maxRecords={_config.MaxRecordsPerPage}";
        if (!string.IsNullOrEmpty(locator))
            url += $"&locator={Uri.EscapeDataString(locator)}";
        return url;
    }

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
}
