namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 device-attestation request context passed to pluggable validators.</summary>
/// <param name="ProductId">Requested product identifier.</param>
/// <param name="ReleaseId">Requested release identifier when available.</param>
/// <param name="Environment">Requested environment when available.</param>
/// <param name="Operation">Distribution operation being authorized.</param>
/// <param name="Platform">Client platform or attestation provider hint.</param>
/// <param name="Token">Opaque device-attestation token supplied by the SDK.</param>
public sealed record DeviceAttestationContext(
    string ProductId,
    string? ReleaseId,
    string? Environment,
    string Operation,
    string? Platform,
    string? Token);
