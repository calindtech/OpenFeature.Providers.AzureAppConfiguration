using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFeature.Providers.AzureAppConfiguration.Caching;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace OpenFeature.Providers.AzureAppConfiguration.Extensions
{
    
        public static class ServiceCollectionExtensions
        {
            /// <summary>
            /// Adds the OpenFeature Azure App Configuration provider to the service collection
            /// </summary>
            /// <param name="services">The service collection</param>
            /// <param name="configuration">The configuration instance</param>
            /// <param name="connectionString">Azure App Configuration connection string</param>
            /// <param name="configureCache">Optional cache configuration</param>
            /// <returns>The service collection for chaining</returns>
            public static IServiceCollection AddOpenFeatureAzureProvider(
                this IServiceCollection services,
                IConfiguration configuration,
                string connectionString,
                Action<CacheOptions>? configureCache = null)
            {
                if (services == null) throw new ArgumentNullException(nameof(services));
                if (configuration == null) throw new ArgumentNullException(nameof(configuration));
                if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

                // Configure cache options
                if (configureCache != null)
                {
                    services.Configure(configureCache);
                }
                else
                {
                    services.Configure<CacheOptions>(options => { });
                }
                
                // Register Azure App Configuration
                services.AddAzureAppConfiguration();

                // Add required logging services
                services.TryAddSingleton<ILoggerFactory, LoggerFactory>();
                services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
                
                // Add Feature Management
                services.AddFeatureManagement();
                
                // Register the provider as singleton
                services.TryAddSingleton<AzureAppConfigurationProvider>(sp =>
                {
                    var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>();
                    var logger = sp.GetRequiredService<ILogger<AzureAppConfigurationProvider>>();

                    return new AzureAppConfigurationProvider(
                        configuration,
                        connectionString,
                        cacheOptions,
                        logger);
                });

                // Register hosted service for OpenFeature setup
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OpenFeatureProviderSetup>(sp =>
                {
                    var provider = sp.GetRequiredService<AzureAppConfigurationProvider>();
                    var logger = sp.GetRequiredService<ILogger<OpenFeatureProviderSetup>>();

                    return new OpenFeatureProviderSetup(provider, logger);
                }));

                return services;
        }
        }
        internal class OpenFeatureProviderSetup : IHostedService
        {
            private readonly AzureAppConfigurationProvider _provider;
            private readonly ILogger<OpenFeatureProviderSetup>? _logger;

            public OpenFeatureProviderSetup(
                AzureAppConfigurationProvider provider,
                ILogger<OpenFeatureProviderSetup>? logger = null)
            {
                _provider = provider;
                _logger = logger;
            }

            // Keep CancellationToken to satisfy IHostedService interface
            public async Task StartAsync(CancellationToken cancellationToken)
            {
                try
                {
                    _logger?.LogInformation("Initializing OpenFeature Azure App Configuration provider");
                    await Api.Instance.SetProviderAsync(_provider);
                    _logger?.LogInformation("OpenFeature Azure App Configuration provider initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize OpenFeature Azure App Configuration provider");
                    throw;
                }
            }

            // Keep CancellationToken to satisfy IHostedService interface
            public async Task StopAsync(CancellationToken cancellationToken)
            {
                try
                {
                    _logger?.LogInformation("Shutting down OpenFeature Azure App Configuration provider");
                    await Api.Instance.ShutdownAsync();
                    _logger?.LogInformation("OpenFeature Azure App Configuration provider shut down successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during OpenFeature Azure App Configuration provider shutdown");
                    throw;
                }
            }
        }
}
