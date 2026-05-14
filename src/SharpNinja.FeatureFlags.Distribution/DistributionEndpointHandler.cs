using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class DistributionEndpointHandler
{
    private static readonly Action<ILogger, string, string, string, Exception?> ManifestLookupMissed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(ManifestLookupMissed)),
            "Manifest lookup missed for {ProductId}/{ReleaseId}/{Environment}.");

    private static readonly Action<ILogger, Exception?> MalformedExposureBatchRejected =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(MalformedExposureBatchRejected)),
            "Rejected malformed exposure batch.");

    private static readonly Action<ILogger, string, Exception?> MissingApiKeyRejected =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(MissingApiKeyRejected)),
            "Rejected Distribution request for {ProductId}: missing API key.");

    private readonly IDistributionManifestRegistry manifestRegistry;
    private readonly IProductApiKeyValidator apiKeyValidator;
    private readonly IExposureEventStore exposureEventStore;
    private readonly IOptions<SharpNinjaDistributionOptions> options;
    private readonly ILogger<DistributionEndpointHandler> logger;

    public DistributionEndpointHandler(
        IDistributionManifestRegistry manifestRegistry,
        IProductApiKeyValidator apiKeyValidator,
        IExposureEventStore exposureEventStore,
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<DistributionEndpointHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestRegistry);
        ArgumentNullException.ThrowIfNull(apiKeyValidator);
        ArgumentNullException.ThrowIfNull(exposureEventStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.manifestRegistry = manifestRegistry;
        this.apiKeyValidator = apiKeyValidator;
        this.exposureEventStore = exposureEventStore;
        this.options = options;
        this.logger = logger;
    }

    public async Task<IResult> GetManifestAsync(
        HttpContext context,
        string productId,
        string releaseId,
        string? environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await AuthenticateProductAsync(context, productId, cancellationToken))
        {
            return Results.Unauthorized();
        }

        string resolvedEnvironment = NormalizeEnvironment(environment);
        DistributionManifest? manifest = await FindManifestAsync(
            productId,
            releaseId,
            resolvedEnvironment,
            cancellationToken);

        if (manifest is null)
        {
            ManifestLookupMissed(
                logger,
                productId,
                releaseId,
                resolvedEnvironment,
                null);
            return Results.NotFound();
        }

        if (TryGetIfNoneMatch(context, out string? ifNoneMatch)
            && DistributionManifest.MatchesETag(ifNoneMatch, manifest.ETag))
        {
            ManifestJsonResult.ApplyHeaders(context.Response, manifest);
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return new ManifestJsonResult(manifest);
    }

    public async Task<IResult> GetManifestDeltaAsync(
        HttpContext context,
        string productId,
        string releaseId,
        string? environment,
        string? since,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await AuthenticateProductAsync(context, productId, cancellationToken))
        {
            return Results.Unauthorized();
        }

        DistributionManifest? manifest = await FindManifestAsync(
            productId,
            releaseId,
            NormalizeEnvironment(environment),
            cancellationToken);

        if (manifest is null)
        {
            return Results.NotFound();
        }

        if (manifest.IsNotModifiedSince(since))
        {
            ManifestJsonResult.ApplyHeaders(context.Response, manifest);
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return new ManifestJsonResult(manifest);
    }

    public async Task<IResult> PostExposureAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        ExposureBatchRequest batch;
        try
        {
            batch = await ReadExposureBatchAsync(context, cancellationToken);
        }
        catch (JsonException ex)
        {
            MalformedExposureBatchRejected(logger, ex);
            return JsonText(StatusCodes.Status400BadRequest, "invalid_exposure_batch");
        }

        if (!await AuthenticateProductAsync(context, batch.ProductId, cancellationToken))
        {
            return Results.Unauthorized();
        }

        int accepted = await exposureEventStore.AppendAsync(batch, cancellationToken);
        return Results.Text(
            string.Create(CultureInfo.InvariantCulture, $"{{\"accepted\":{accepted}}}"),
            "application/json",
            statusCode: StatusCodes.Status202Accepted);
    }

    public static IResult GetHealth() =>
        Results.Text(
            "{\"status\":\"ok\"}",
            "application/json",
            statusCode: StatusCodes.Status200OK);

    public IResult GetMetrics()
    {
        string metrics = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            # HELP sharpninja_distribution_manifests Registered in-memory manifests.
            # TYPE sharpninja_distribution_manifests gauge
            sharpninja_distribution_manifests {manifestRegistry.Count}
            # HELP sharpninja_distribution_exposure_events_total Buffered exposure events accepted by the Distribution service.
            # TYPE sharpninja_distribution_exposure_events_total counter
            sharpninja_distribution_exposure_events_total {exposureEventStore.Count}
            """);

        return Results.Text(metrics, "text/plain; version=0.0.4", statusCode: StatusCodes.Status200OK);
    }

    private async ValueTask<DistributionManifest?> FindManifestAsync(
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken)
    {
        return await manifestRegistry.FindAsync(
            productId,
            releaseId,
            environment,
            cancellationToken);
    }

    private async ValueTask<bool> AuthenticateProductAsync(
        HttpContext context,
        string productId,
        CancellationToken cancellationToken)
    {
        string? apiKey = ReadApiKey(context);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MissingApiKeyRejected(logger, productId, null);
            return false;
        }

        return await apiKeyValidator.ValidateAsync(productId, apiKey, cancellationToken);
    }

    private string NormalizeEnvironment(string? environment)
    {
        if (!string.IsNullOrWhiteSpace(environment))
        {
            return environment;
        }

        string configuredDefault = options.Value.DefaultEnvironment;
        return string.IsNullOrWhiteSpace(configuredDefault) ? "Development" : configuredDefault;
    }

    private static bool TryGetIfNoneMatch(HttpContext context, out string ifNoneMatch)
    {
        if (context.Request.Headers.TryGetValue("If-None-Match", out StringValues values))
        {
            ifNoneMatch = values.ToString();
            return !string.IsNullOrWhiteSpace(ifNoneMatch);
        }

        ifNoneMatch = string.Empty;
        return false;
    }

    private static string? ReadApiKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(SharpNinjaDistributionHeaders.ProductApiKeyHeaderName, out StringValues productValues)
            && !string.IsNullOrWhiteSpace(productValues.ToString()))
        {
            return productValues.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Api-Key", out StringValues values)
            && !string.IsNullOrWhiteSpace(values.ToString()))
        {
            return values.ToString();
        }

        return null;
    }

    private async ValueTask<ExposureBatchRequest> ReadExposureBatchAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
        return ExposureBatchRequest.Parse(document.RootElement, NormalizeEnvironment(null));
    }

    private static IResult JsonText(int statusCode, string code) =>
        Results.Text(
            string.Create(CultureInfo.InvariantCulture, $"{{\"error\":\"{code}\"}}"),
            "application/json",
            statusCode: statusCode);
}
