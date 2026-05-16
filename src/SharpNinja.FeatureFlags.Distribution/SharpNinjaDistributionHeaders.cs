namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 v1 well-known HTTP headers for Distribution service clients.</summary>
/// <remarks>
/// Static constants. Header names are part of the public wire contract; do not rename.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
public static class SharpNinjaDistributionHeaders
{
    /// <summary>TR-9 v1 header name used for product-scoped Distribution API keys.</summary>
    public const string ProductApiKeyHeaderName = "X-SharpNinja-Api-Key";

    /// <summary>TR-9 v1 header name used for opaque device-attestation tokens.</summary>
    public const string DeviceAttestationTokenHeaderName = "X-SharpNinja-Device-Attestation";

    /// <summary>TR-9 v1 header name used to identify the attestation platform or provider.</summary>
    public const string DevicePlatformHeaderName = "X-SharpNinja-Device-Platform";
}
