using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Exceptions;
using SalesforceBulkApi.Models;
using Xunit;

namespace SalesforceBulkApi.Tests.Auth;

public class SalesforceAuthServiceTests
{
    private static SalesforceConfig BuildConfig() => new()
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        LoginUrl = "https://login.salesforce.com"
    };

    private static HttpClient BuildHttpClient(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(response);
        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Success_ReturnsToken()
    {
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = "my-token",
            instance_url = "https://na1.salesforce.com",
            token_type = "Bearer"
        });

        var httpClient = BuildHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tokenJson, System.Text.Encoding.UTF8, "application/json")
        });

        var svc = new SalesforceAuthService(BuildConfig(), httpClient, NullLogger<SalesforceAuthService>.Instance);

        var token = await svc.GetAccessTokenAsync();

        Assert.Equal("my-token", token);
    }

    [Fact]
    public async Task GetInstanceUrlAsync_Success_ReturnsInstanceUrl()
    {
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = "my-token",
            instance_url = "https://na1.salesforce.com",
            token_type = "Bearer"
        });

        var httpClient = BuildHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tokenJson, System.Text.Encoding.UTF8, "application/json")
        });

        var svc = new SalesforceAuthService(BuildConfig(), httpClient, NullLogger<SalesforceAuthService>.Instance);

        var url = await svc.GetInstanceUrlAsync();

        Assert.Equal("https://na1.salesforce.com", url);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_DoesNotCallHttpTwice()
    {
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = "cached-token",
            instance_url = "https://na1.salesforce.com",
            token_type = "Bearer"
        });

        int callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() =>
               {
                   callCount++;
                   return new HttpResponseMessage(HttpStatusCode.OK)
                   {
                       Content = new StringContent(tokenJson, System.Text.Encoding.UTF8, "application/json")
                   };
               });

        var svc = new SalesforceAuthService(BuildConfig(), new HttpClient(handler.Object), NullLogger<SalesforceAuthService>.Instance);

        var t1 = await svc.GetAccessTokenAsync();
        var t2 = await svc.GetAccessTokenAsync();

        Assert.Equal("cached-token", t1);
        Assert.Equal("cached-token", t2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HttpError_ThrowsSalesforceAuthException()
    {
        var httpClient = BuildHttpClient(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"invalid_client\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var svc = new SalesforceAuthService(BuildConfig(), httpClient, NullLogger<SalesforceAuthService>.Instance);

        await Assert.ThrowsAsync<SalesforceAuthException>(() => svc.GetAccessTokenAsync());
    }

    [Fact]
    public async Task RefreshTokenAsync_ErrorInResponse_ThrowsSalesforceAuthException()
    {
        var errorJson = JsonSerializer.Serialize(new
        {
            error = "invalid_grant",
            error_description = "authentication failure"
        });

        var httpClient = BuildHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
        });

        var svc = new SalesforceAuthService(BuildConfig(), httpClient, NullLogger<SalesforceAuthService>.Instance);

        await Assert.ThrowsAsync<SalesforceAuthException>(() => svc.RefreshTokenAsync());
    }
}
