namespace SalesforceBulkApi.Auth;

/// <summary>
/// Provides Salesforce OAuth2 authentication and token management.
/// </summary>
public interface ISalesforceAuthService
{
    /// <summary>
    /// Returns a valid access token, obtaining or refreshing one as needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid OAuth2 access token string.</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Salesforce instance URL obtained during authentication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Salesforce instance URL (e.g. https://na1.salesforce.com).</returns>
    Task<string> GetInstanceUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the current access token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshTokenAsync(CancellationToken cancellationToken = default);
}
