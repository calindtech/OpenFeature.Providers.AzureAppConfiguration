using Azure.Data.AppConfiguration;
using Microsoft.FeatureManagement;

namespace OpenFeature.Providers.AzureAppConfiguration
{
    public class AzureAppConfigurationOptions
    {
        /// <summary>
        /// The prefix used for feature flags in Azure App Configuration
        /// Default: "FeatureManagement:"
        /// </summary>
        public string FeatureFlagPrefix { get; set; } = "FeatureManagement:";

        /// <summary>
        /// How often to refresh the feature flags from Azure App Configuration
        /// Default: 5 minutes
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Whether to automatically refresh feature flags in the background
        /// Default: true
        /// </summary>
        public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// The Azure App Configuration label to use
        /// Default: null (no label)
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// The cache expiration interval for feature flags
        /// Default: 30 seconds
        /// </summary>
        public TimeSpan CacheExpirationInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to send telemetry to Application Insights
        /// Default: false
        /// </summary>
        public bool SendTelemetry { get; set; } = false;

        /// <summary>
        /// Whether to cache feature flags locally when offline
        /// Default: true
        /// </summary>
        public bool EnableOfflineCache { get; set; } = true;

        /// <summary>
        /// The maximum number of retries when fetching feature flags
        /// Default: 3
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// The retry delay between attempts
        /// Default: 1 second
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Optional configuration for the feature flag client
        /// </summary>
        public Action<ConfigurationClient>? ConfigureClient { get; set; }

        /// <summary>
        /// Optional configuration for Azure App Configuration
        /// </summary>
        public Action<AzureAppConfigurationOptions>? ConfigureAzureAppConfiguration { get; set; }

        /// <summary>
        /// Optional configuration for feature management
        /// </summary>
        public Action<IFeatureManagementBuilder>? ConfigureFeatureManagement { get; set; }

        /// <summary>
        /// Validates the options
        /// </summary>
        public void Validate()
        {
            if (RefreshInterval <= TimeSpan.Zero)
            {
                throw new ArgumentException("RefreshInterval must be greater than zero.");
            }

            if (CacheExpirationInterval <= TimeSpan.Zero)
            {
                throw new ArgumentException("CacheExpirationInterval must be greater than zero.");
            }

            if (MaxRetries < 0)
            {
                throw new ArgumentException("MaxRetries must be greater than or equal to zero.");
            }

            if (RetryDelay <= TimeSpan.Zero)
            {
                throw new ArgumentException("RetryDelay must be greater than zero.");
            }
        }

        /// <summary>
        /// Creates a ConfigurationClient settings based on the options
        /// </summary>
        internal ConfigurationClientOptions CreateClientOptions()
        {
            return new ConfigurationClientOptions
            {
                Retry =
                {
                    MaxRetries = MaxRetries,
                    Delay = RetryDelay
                }
            };
        }
    }

    /// <summary>
    /// Extension methods for AzureAppConfigurationOptions
    /// </summary>
    public static class AzureAppConfigurationOptionsExtensions
    {
        /// <summary>
        /// Configures the feature flag client
        /// </summary>
        public static AzureAppConfigurationOptions ConfigureFeatureFlagClient(
            this AzureAppConfigurationOptions options,
            Action<ConfigurationClient> configure)
        {
            options.ConfigureClient = configure;
            return options;
        }

        /// <summary>
        /// Sets up feature management options
        /// </summary>
        public static AzureAppConfigurationOptions ConfigureFeatures(
            this AzureAppConfigurationOptions options,
            Action<IFeatureManagementBuilder> configure)
        {
            options.ConfigureFeatureManagement = configure;
            return options;
        }

        /// <summary>
        /// Configures Azure App Configuration options
        /// </summary>
        public static AzureAppConfigurationOptions ConfigureAzureOptions(
            this AzureAppConfigurationOptions options,
            Action<AzureAppConfigurationOptions> configure)
        {
            options.ConfigureAzureAppConfiguration = configure;
            return options;
        }
    }
}