namespace OpenFeature.Providers.AzureAppConfiguration.Caching
{
    internal class CachedValue<T>
    {
        public required T Value { get; set; }
        public required string Variant { get; set; }
        public required string Reason { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
