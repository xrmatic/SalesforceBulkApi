using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SalesforceBulkApi.Exceptions;
using SalesforceBulkApi.Models;

namespace SalesforceBulkApi.Auth;

/// <summary>
/// Authenticates with Salesforce using the OAuth2 client_credentials flow and caches the
/// resulting token until it needs to be refreshed.
/// </summary>
public class SalesforceAuthService : ISalesforceAuthService
{
    private readonly SalesforceConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SalesforceAuthService> _logger;

    private AuthToken? _currentToken;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public SalesforceAuthService(
        SalesforceConfig config,
        HttpClient httpClient,
        ILogger<SalesforceAuthService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_currentToken is not null && !string.IsNullOrEmpty(_currentToken.AccessToken))
            return _currentToken.AccessToken;

        await RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        return _currentToken!.AccessToken;
    }

    /// <inheritdoc/>
    public async Task<string> GetInstanceUrlAsync(CancellationToken cancellationToken = default)
    {
        if (_currentToken is not null && !string.IsNullOrEmpty(_currentToken.InstanceUrl))
            return _currentToken.InstanceUrl;

        await RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        return _currentToken!.InstanceUrl;
    }

    /// <inheritdoc/>
    public async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Authenticating with Salesforce at {LoginUrl}.", _config.LoginUrl);

            var tokenUrl = $"{_config.LoginUrl.TrimEnd('/')}/services/oauth2/token";
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret)
            });

            var response = await _httpClient
                .PostAsync(tokenUrl, formContent, cancellationToken)
                .ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Authentication failed with status {Status}: {Body}", response.StatusCode, responseBody);
                throw new SalesforceAuthException(
                    $"Authentication failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
            }

            var token = JsonSerializer.Deserialize<AuthToken>(responseBody, JsonOptions.Default)
                        ?? throw new SalesforceAuthException("Empty or invalid token response from Salesforce.");

            if (!string.IsNullOrEmpty(token.Error))
                throw new SalesforceAuthException($"Salesforce auth error '{token.Error}': {token.ErrorDescription}");

            _currentToken = token;
            _logger.LogInformation("Successfully authenticated. Instance URL: {InstanceUrl}", _currentToken.InstanceUrl);
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
