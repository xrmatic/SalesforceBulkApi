using SalesforceBulkApi.Models;
using Xunit;

namespace SalesforceBulkApi.Tests;

public class SalesforceConfigTests
{
    [Fact]
    public void Validate_AllFieldsSet_DoesNotThrow()
    {
        var config = new SalesforceConfig
        {
            ClientId = "id",
            ClientSecret = "secret",
            LoginUrl = "https://login.salesforce.com"
        };

        var ex = Record.Exception(() => config.Validate());
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("", "secret", "https://login.salesforce.com")]
    [InlineData("id", "", "https://login.salesforce.com")]
    [InlineData("id", "secret", "")]
    public void Validate_MissingRequiredField_ThrowsArgumentException(string clientId, string secret, string loginUrl)
    {
        var config = new SalesforceConfig
        {
            ClientId = clientId,
            ClientSecret = secret,
            LoginUrl = loginUrl
        };

        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_InvalidMaxPollAttempts_ThrowsArgumentOutOfRangeException()
    {
        var config = new SalesforceConfig
        {
            ClientId = "id",
            ClientSecret = "secret",
            LoginUrl = "https://login.salesforce.com",
            MaxPollAttempts = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_InvalidPollIntervalMs_ThrowsArgumentOutOfRangeException()
    {
        var config = new SalesforceConfig
        {
            ClientId = "id",
            ClientSecret = "secret",
            LoginUrl = "https://login.salesforce.com",
            PollIntervalMs = -1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_InvalidMaxRecordsPerPage_ThrowsArgumentOutOfRangeException()
    {
        var config = new SalesforceConfig
        {
            ClientId = "id",
            ClientSecret = "secret",
            LoginUrl = "https://login.salesforce.com",
            MaxRecordsPerPage = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }
}
