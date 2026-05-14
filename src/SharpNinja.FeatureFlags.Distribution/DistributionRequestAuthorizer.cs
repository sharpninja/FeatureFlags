using Microsoft.Extensions.Primitives;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class DistributionRequestAuthorizer
{
    private const string ManifestReadOperation = "manifest-read";
    private const string ExposureWriteOperation = "exposure-write";

    private static readonly Action<ILogger, string, Exception?> MissingApiKeyRejected =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4001, nameof(MissingApiKeyRejected)),
            "Rejected Distribution request for {ProductId}: missing API key.");

    private static readonly Action<ILogger, string, string, Exception?> DeviceAttestationRejected =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4002, nameof(DeviceAttestationRejected)),
            "Rejected Distribution request for {ProductId}: device attestation failed with {FailureCode}.");

    private readonly IProductApiKeyValidator apiKeyValidator;
    private readonly IDeviceAttestationPolicy attestationPolicy;
    private readonly IEnumerable<IDeviceAttestationValidator> attestationValidators;
    private readonly DistributionMetrics metrics;
    private readonly ILogger<DistributionRequestAuthorizer> logger;

    public DistributionRequestAuthorizer(
        IProductApiKeyValidator apiKeyValidator,
        IDeviceAttestationPolicy attestationPolicy,
        IEnumerable<IDeviceAttestationValidator> attestationValidators,
        DistributionMetrics metrics,
        ILogger<DistributionRequestAuthorizer> logger)
    {
        ArgumentNullException.ThrowIfNull(apiKeyValidator);
        ArgumentNullException.ThrowIfNull(attestationPolicy);
        ArgumentNullException.ThrowIfNull(attestationValidators);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        this.apiKeyValidator = apiKeyValidator;
        this.attestationPolicy = attestationPolicy;
        this.attestationValidators = attestationValidators;
        this.metrics = metrics;
        this.logger = logger;
    }

    public ValueTask<DistributionAuthorizationResult> AuthorizeManifestReadAsync(
        HttpContext context,
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken) =>
        AuthorizeAsync(context, productId, releaseId, environment, ManifestReadOperation, cancellationToken);

    public ValueTask<DistributionAuthorizationResult> AuthorizeExposureWriteAsync(
        HttpContext context,
        ExposureBatchRequest batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return AuthorizeAsync(
            context,
            batch.ProductId,
            batch.ReleaseId,
            batch.Environment,
            ExposureWriteOperation,
            cancellationToken);
    }

    private async ValueTask<DistributionAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        string productId,
        string? releaseId,
        string? environment,
        string operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? apiKey = ReadApiKey(context);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            metrics.RecordAuthFailure();
            MissingApiKeyRejected(logger, productId, null);
            return DistributionAuthorizationResult.Unauthorized("missing_api_key");
        }

        if (!await apiKeyValidator.ValidateAsync(productId, apiKey, cancellationToken))
        {
            metrics.RecordAuthFailure();
            return DistributionAuthorizationResult.Unauthorized("invalid_api_key");
        }

        metrics.RecordAuthSuccess();

        var attestationContext = new DeviceAttestationContext(
            productId,
            releaseId,
            environment,
            operation,
            ReadHeader(context, SharpNinjaDistributionHeaders.DevicePlatformHeaderName),
            ReadHeader(context, SharpNinjaDistributionHeaders.DeviceAttestationTokenHeaderName));

        DeviceAttestationPolicyDecision policyDecision = await attestationPolicy.EvaluateAsync(
            attestationContext,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(policyDecision.FailureCode))
        {
            metrics.RecordAttestationFailure();
            DeviceAttestationRejected(logger, productId, policyDecision.FailureCode, null);
            return DistributionAuthorizationResult.Forbidden(policyDecision.FailureCode);
        }

        if (!policyDecision.RequiresValidation)
        {
            metrics.RecordAttestationSkipped();
            return DistributionAuthorizationResult.Success;
        }

        foreach (IDeviceAttestationValidator validator in attestationValidators)
        {
            DeviceAttestationValidationResult validation = await validator.ValidateAsync(
                attestationContext,
                cancellationToken);

            if (validation.Succeeded)
            {
                metrics.RecordAttestationSuccess();
                return DistributionAuthorizationResult.Success;
            }
        }

        metrics.RecordAttestationFailure();
        DeviceAttestationRejected(logger, productId, "invalid_device_attestation", null);
        return DistributionAuthorizationResult.Forbidden("invalid_device_attestation");
    }

    private static string? ReadApiKey(HttpContext context)
    {
        string? productApiKey = ReadHeader(context, SharpNinjaDistributionHeaders.ProductApiKeyHeaderName);
        if (!string.IsNullOrWhiteSpace(productApiKey))
        {
            return productApiKey;
        }

        string? genericApiKey = ReadHeader(context, "X-Api-Key");
        if (!string.IsNullOrWhiteSpace(genericApiKey))
        {
            return genericApiKey;
        }

        string? authorization = ReadHeader(context, "Authorization");
        const string bearerPrefix = "Bearer ";
        if (authorization?.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            return authorization[bearerPrefix.Length..].Trim();
        }

        return null;
    }

    private static string? ReadHeader(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out StringValues values)
            && !string.IsNullOrWhiteSpace(values.ToString()))
        {
            return values.ToString();
        }

        return null;
    }
}
