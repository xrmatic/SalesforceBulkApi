namespace SalesforceBulkApi.Models;

/// <summary>
/// Configuration settings for connecting to Salesforce via the Bulk API v2.
/// </summary>
public class SalesforceConfig
{
    /// <summary>
    /// OAuth2 Connected App client ID (consumer key).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 Connected App client secret (consumer secret).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Salesforce login URL. Use https://test.salesforce.com for sandbox.
    /// Defaults to https://login.salesforce.com.
    /// </summary>
    public string LoginUrl { get; set; } = "https://login.salesforce.com";

    /// <summary>
    /// Salesforce API version to use (e.g. "v59.0").
    /// Defaults to v59.0.
    /// </summary>
    public string ApiVersion { get; set; } = "v59.0";

    /// <summary>
    /// Maximum number of times to poll for job completion before giving up.
    /// Defaults to 60.
    /// </summary>
    public int MaxPollAttempts { get; set; } = 60;

    /// <summary>
    /// Milliseconds to wait between job status polls.
    /// Defaults to 5000 ms.
    /// </summary>
    public int PollIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of records to request per query results page.
    /// Defaults to 50000.
    /// </summary>
    public int MaxRecordsPerPage { get; set; } = 50_000;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// Defaults to 120 seconds.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Validates that all required fields are set and throws if not.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("ClientId is required.", nameof(ClientId));
        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new ArgumentException("ClientSecret is required.", nameof(ClientSecret));
        if (string.IsNullOrWhiteSpace(LoginUrl))
            throw new ArgumentException("LoginUrl is required.", nameof(LoginUrl));
        if (MaxPollAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxPollAttempts), "MaxPollAttempts must be > 0.");
        if (PollIntervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(PollIntervalMs), "PollIntervalMs must be > 0.");
        if (MaxRecordsPerPage <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRecordsPerPage), "MaxRecordsPerPage must be > 0.");
    }
}
