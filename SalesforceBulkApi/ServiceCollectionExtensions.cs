using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesforceBulkApi.Auth;
using SalesforceBulkApi.Models;
using SalesforceBulkApi.Services;

namespace SalesforceBulkApi;

/// <summary>
/// Extension methods for registering Salesforce Bulk API services with
/// Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SalesforceBulkClient"/> and all required services
    /// with the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Salesforce connection configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSalesforceBulkApi(
        this IServiceCollection services,
        SalesforceConfig config)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (config is null) throw new ArgumentNullException(nameof(config));
        config.Validate();

        services.AddSingleton(config);

        services.AddHttpClient<ISalesforceAuthService, SalesforceAuthService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
        });

        services.AddHttpClient<IBulkApiService, BulkApiService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
        });

        services.AddHttpClient<IQueryService, QueryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
        });

        services.AddTransient<SalesforceBulkClient>();

        return services;
    }
}
