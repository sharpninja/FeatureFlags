namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 v1 well-known HTTP headers for Distribution service clients.</summary>
public static class SharpNinjaDistributionHeaders
{
    /// <summary>TR-9 v1 header name used for product-scoped Distribution API keys.</summary>
    public const string ProductApiKeyHeaderName = "X-SharpNinja-Api-Key";
}
