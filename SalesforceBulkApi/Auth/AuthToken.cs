using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Auth;

/// <summary>
/// OAuth2 access token response from Salesforce.
/// </summary>
internal class AuthToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("instance_url")]
    public string InstanceUrl { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("issued_at")]
    public string? IssuedAt { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
