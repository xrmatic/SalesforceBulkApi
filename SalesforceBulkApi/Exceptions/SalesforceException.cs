namespace SalesforceBulkApi.Exceptions;

/// <summary>
/// Base exception for all Salesforce API errors.
/// </summary>
public class SalesforceException : Exception
{
    public SalesforceException(string message) : base(message) { }
    public SalesforceException(string message, Exception inner) : base(message, inner) { }
}
