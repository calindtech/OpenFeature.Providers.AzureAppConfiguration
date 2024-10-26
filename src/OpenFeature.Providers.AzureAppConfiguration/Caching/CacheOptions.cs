namespace OpenFeature.Providers.AzureAppConfiguration.Caching
{
    public class CacheOptions
    {
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxItems { get; set; } = 1000;
        public bool EnableRefreshBackground { get; set; } = true;
        public TimeSpan BackgroundRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

        public CacheOptions()
        {
            // Default constructor with default values
        }

        public CacheOptions(TimeSpan defaultTtl, int maxItems, bool enableRefreshBackground, TimeSpan backgroundRefreshInterval)
        {
            DefaultTtl = defaultTtl;
            MaxItems = maxItems;
            EnableRefreshBackground = enableRefreshBackground;
            BackgroundRefreshInterval = backgroundRefreshInterval;
        }
    }
}
