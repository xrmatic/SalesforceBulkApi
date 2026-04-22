using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Models;
using SalesforceBulkApi.Services;

namespace SalesforceBulkApi;

/// <summary>
/// Creates a <see cref="SalesforceBulkClient"/> without a DI container.
/// Suitable for console apps, Azure Functions, or any lightweight scenario.
/// </summary>
public static class SalesforceBulkClientFactory
{
    /// <summary>
    /// Creates a fully configured <see cref="SalesforceBulkClient"/> using
    /// the supplied configuration. Uses <see cref="NullLoggerFactory"/> unless
    /// a custom logger factory is provided.
    /// </summary>
    /// <param name="config">Salesforce connection settings.</param>
    /// <param name="loggerFactory">
    /// Optional logger factory. Defaults to <see cref="NullLoggerFactory"/>.
    /// </param>
    public static SalesforceBulkClient Create(
        SalesforceConfig config,
        ILoggerFactory? loggerFactory = null)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));
        config.Validate();

        loggerFactory ??= NullLoggerFactory.Instance;

        var timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);

        var authHttpClient = new HttpClient { Timeout = timeout };
        var authService = new SalesforceAuthService(
            config,
            authHttpClient,
            loggerFactory.CreateLogger<SalesforceAuthService>());

        var bulkHttpClient = new HttpClient { Timeout = timeout };
        var bulkApiService = new BulkApiService(
            config,
            authService,
            bulkHttpClient,
            loggerFactory.CreateLogger<BulkApiService>());

        var queryHttpClient = new HttpClient { Timeout = timeout };
        var queryService = new QueryService(
            config,
            authService,
            queryHttpClient,
            loggerFactory.CreateLogger<QueryService>());

        return new SalesforceBulkClient(bulkApiService, queryService);
    }
}
