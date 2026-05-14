namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 TR-6 TR-9 TR-10 TR-11 v1 result from a remote manifest fetch transport.</summary>
/// <param name="Envelope">Fetched signed manifest envelope when a remote source returned content.</param>
/// <param name="NotModified">Indicates the remote source reported that the active ETag is unchanged.</param>
/// <param name="ErrorMessage">Optional transport failure detail.</param>
/// <param name="NotConfigured">Indicates no remote manifest endpoint is configured.</param>
public sealed record RemoteManifestFetchResult(
    SignedManifestEnvelope? Envelope,
    bool NotModified = false,
    string? ErrorMessage = null,
    bool NotConfigured = false);
