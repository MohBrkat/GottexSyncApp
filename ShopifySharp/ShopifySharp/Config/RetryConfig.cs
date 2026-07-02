namespace ShopifySharp.Config
{
    public class RetryConfig
    {
        public int MaxRetryLimit { get; } = 5;

        public int BaseDelayMs { get; } = 300000;
    }
}
