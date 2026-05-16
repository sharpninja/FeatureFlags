namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-7 TR-10 TR-11 Phase 0 contract: DI-resolved administrative surface for feature flags.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaFeatureFlagAdmin
{
    /// <summary>Raised when the active manifest changes.</summary>
    event EventHandler<ManifestUpdatedEventArgs>? ManifestUpdated;

    /// <summary>Requests a normal manifest refresh.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the refresh request.</returns>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests a kill-switch manifest refresh that bypasses normal cache cadence.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the refresh request.</returns>
    ValueTask ForceRefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current diagnostic snapshot.</summary>
    /// <returns>Diagnostic snapshot.</returns>
    DiagnosticSnapshot GetDiagnostics();
}
