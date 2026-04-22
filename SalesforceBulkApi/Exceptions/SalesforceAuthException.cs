namespace SalesforceBulkApi.Exceptions;

/// <summary>
/// Thrown when OAuth2 authentication with Salesforce fails.
/// </summary>
public class SalesforceAuthException : SalesforceException
{
    public SalesforceAuthException(string message) : base(message) { }
    public SalesforceAuthException(string message, Exception inner) : base(message, inner) { }
}
