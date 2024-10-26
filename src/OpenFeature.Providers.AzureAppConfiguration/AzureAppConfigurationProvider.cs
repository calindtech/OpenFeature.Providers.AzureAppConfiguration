using OpenFeature.Model;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OpenFeature.Providers.AzureAppConfiguration.Caching;
using System.Collections.Concurrent;
using OpenFeature.Constant;

namespace OpenFeature.Providers.AzureAppConfiguration
{
    public class AzureAppConfigurationProvider : FeatureProvider, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationClient _client;
        private readonly IMemoryCache _cache;
        private readonly CacheOptions _cacheOptions;
        private readonly Timer? _refreshTimer;
        private readonly ConcurrentDictionary<string, DateTime> _lastRefreshTimes;
        private readonly ILogger<AzureAppConfigurationProvider> _logger;
        private bool _disposed;

        public AzureAppConfigurationProvider(
            IConfiguration configuration,
            string connectionString,
            IOptions<CacheOptions> cacheOptions,
            ILogger<AzureAppConfigurationProvider> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _client = new ConfigurationClient(connectionString);
            _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Initializing Azure App Configuration provider with cache size: {MaxItems}", _cacheOptions.MaxItems);

            var memoryCacheOptions = new MemoryCacheOptions
            {
                SizeLimit = _cacheOptions.MaxItems
            };

            _cache = new MemoryCache(memoryCacheOptions);
            _lastRefreshTimes = new ConcurrentDictionary<string, DateTime>();

            if (_cacheOptions.EnableRefreshBackground)
            {
                _logger.LogInformation("Enabling background refresh with interval: {Interval}", _cacheOptions.BackgroundRefreshInterval);
                _refreshTimer = new Timer(
                    RefreshCache,
                    null,
                    _cacheOptions.BackgroundRefreshInterval,
                    _cacheOptions.BackgroundRefreshInterval);
            }
        }

        public override Metadata GetMetadata()
        {
            return new Metadata("azure-app-configuration-provider");
        }

        private async void RefreshCache(object? state)
        {
            _logger.LogDebug("Starting background cache refresh for {Count} keys", _lastRefreshTimes.Count);

            foreach (var key in _lastRefreshTimes.Keys)
            {
                try
                {
                    var lastRefresh = _lastRefreshTimes.GetValueOrDefault(key);
                    if (DateTime.UtcNow - lastRefresh > _cacheOptions.DefaultTtl.Subtract(TimeSpan.FromMinutes(1)))
                    {
                        await RefreshCacheValueAsync(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing cache for key: {Key}", key);
                }
            }

            _logger.LogDebug("Completed background cache refresh");
        }

        private async Task RefreshCacheValueAsync(string key)
        {
            try
            {
                _logger.LogDebug("Refreshing cache for key: {Key}", key);
                var setting = await _client.GetConfigurationSettingAsync($"FeatureManagement:{key}");
                if (setting?.Value != null)
                {
                    _lastRefreshTimes.AddOrUpdate(key, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
                    _logger.LogDebug("Successfully refreshed cache for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh cache for key: {Key}", key);
            }
        }

        private async Task<ResolutionDetails<T>> ResolveFromCacheOrFetchAsync<T>(
            string flagKey,
            T defaultValue,
            Func<string, Task<T>> fetchFunc,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(flagKey))
            {
                throw new ArgumentNullException(nameof(flagKey));
            }

            var cacheKey = $"feature_{flagKey}_{typeof(T).Name}";

            try
            {
                if (_cache.TryGetValue(cacheKey, out CachedValue<T> cachedValue))
                {
                    _logger.LogDebug("Cache hit for key: {Key}, type: {Type}", flagKey, typeof(T).Name);
                    return new ResolutionDetails<T>(
                        flagKey,
                        cachedValue.Value,
                        ErrorType.None,
                        null,
                        cachedValue.Variant,
                        "CACHED");
                }

                _logger.LogDebug("Cache miss for key: {Key}, type: {Type}, fetching from Azure App Configuration",
                    flagKey, typeof(T).Name);

                var value = await fetchFunc($"FeatureManagement:{flagKey}");

                var newCachedValue = new CachedValue<T>
                {
                    Value = value,
                    Variant = value?.ToString() ?? "null",
                    Reason = "RETRIEVED",
                    ExpiresAt = DateTime.UtcNow.Add(_cacheOptions.DefaultTtl)
                };

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetAbsoluteExpiration(newCachedValue.ExpiresAt);

                _cache.Set(cacheKey, newCachedValue, cacheEntryOptions);
                _lastRefreshTimes.AddOrUpdate(flagKey, DateTime.UtcNow, (_, _) => DateTime.UtcNow);

                _logger.LogDebug("Successfully fetched and cached value for key: {Key}, type: {Type}",
                    flagKey, typeof(T).Name);

                return new ResolutionDetails<T>(
                    flagKey,
                    value,
                    ErrorType.None,
                    null,
                    newCachedValue.Variant,
                    "RETRIEVED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving flag value for key: {Key}, type: {Type}",
                    flagKey, typeof(T).Name);

                return new ResolutionDetails<T>(
                    flagKey,
                    defaultValue,
                    ErrorType.General,
                    ex.Message,
                    "error",
                    "ERROR");
            }
        }

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(
            string flagKey,
            bool defaultValue,
            EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Resolving boolean flag: {Key}, default: {DefaultValue}", flagKey, defaultValue);

            return ResolveFromCacheOrFetchAsync(
                flagKey,
                defaultValue,
                async (key) => _configuration.GetValue<bool>(key, defaultValue),
                cancellationToken);
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(
            string flagKey,
            string defaultValue,
            EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Resolving string flag: {Key}, default: {DefaultValue}", flagKey, defaultValue);

            return ResolveFromCacheOrFetchAsync(
                flagKey,
                defaultValue,
                async (key) => _configuration.GetValue<string>(key, defaultValue) ?? defaultValue,
                cancellationToken);
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(
            string flagKey,
            int defaultValue,
            EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Resolving integer flag: {Key}, default: {DefaultValue}", flagKey, defaultValue);

            return ResolveFromCacheOrFetchAsync(
                flagKey,
                defaultValue,
                async (key) => _configuration.GetValue<int>(key, defaultValue),
                cancellationToken);
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(
            string flagKey,
            double defaultValue,
            EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Resolving double flag: {Key}, default: {DefaultValue}", flagKey, defaultValue);

            return ResolveFromCacheOrFetchAsync(
                flagKey,
                defaultValue,
                async (key) => _configuration.GetValue<double>(key, defaultValue),
                cancellationToken);
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(
            string flagKey,
            Value defaultValue,
            EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Resolving structure flag: {Key}", flagKey);

            return ResolveFromCacheOrFetchAsync(
                flagKey,
                defaultValue,
                async (key) => _configuration.GetSection(key).Get<Value>() ?? defaultValue,
                cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogInformation("Disposing Azure App Configuration provider");
                    _refreshTimer?.Dispose();
                    _cache?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}