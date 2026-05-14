using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

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

    private readonly IDistributionManifestRegistry manifestRegistry;
    private readonly IExposureEventStore exposureEventStore;
    private readonly DistributionRequestAuthorizer authorizer;
    private readonly DistributionMetrics metrics;
    private readonly IOptions<SharpNinjaDistributionOptions> options;
    private readonly ILogger<DistributionEndpointHandler> logger;

    public DistributionEndpointHandler(
        IDistributionManifestRegistry manifestRegistry,
        IExposureEventStore exposureEventStore,
        DistributionRequestAuthorizer authorizer,
        DistributionMetrics metrics,
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<DistributionEndpointHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestRegistry);
        ArgumentNullException.ThrowIfNull(exposureEventStore);
        ArgumentNullException.ThrowIfNull(authorizer);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.manifestRegistry = manifestRegistry;
        this.exposureEventStore = exposureEventStore;
        this.authorizer = authorizer;
        this.metrics = metrics;
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

        string resolvedEnvironment = NormalizeEnvironment(environment);
        DistributionAuthorizationResult authorization = await authorizer.AuthorizeManifestReadAsync(
            context,
            productId,
            releaseId,
            resolvedEnvironment,
            cancellationToken);
        if (!authorization.Succeeded)
        {
            return AuthorizationFailure(authorization);
        }

        DistributionManifest? manifest = await FindManifestAsync(
            productId,
            releaseId,
            resolvedEnvironment,
            cancellationToken);

        if (manifest is null)
        {
            metrics.RecordManifestCacheMiss();
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
            metrics.RecordManifestNotModified();
            ManifestJsonResult.ApplyHeaders(context.Response, manifest, options.Value);
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        metrics.RecordManifestCacheHit();
        return new ManifestJsonResult(manifest, options.Value);
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

        string resolvedEnvironment = NormalizeEnvironment(environment);
        DistributionAuthorizationResult authorization = await authorizer.AuthorizeManifestReadAsync(
            context,
            productId,
            releaseId,
            resolvedEnvironment,
            cancellationToken);
        if (!authorization.Succeeded)
        {
            return AuthorizationFailure(authorization);
        }

        DistributionManifest? manifest = await FindManifestAsync(
            productId,
            releaseId,
            resolvedEnvironment,
            cancellationToken);

        if (manifest is null)
        {
            metrics.RecordManifestCacheMiss();
            return Results.NotFound();
        }

        if (manifest.IsNotModifiedSince(since))
        {
            metrics.RecordManifestNotModified();
            ManifestJsonResult.ApplyHeaders(context.Response, manifest, options.Value);
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        metrics.RecordManifestCacheHit();
        return new ManifestJsonResult(manifest, options.Value);
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

        DistributionAuthorizationResult authorization = await authorizer.AuthorizeExposureWriteAsync(
            context,
            batch,
            cancellationToken);
        if (!authorization.Succeeded)
        {
            return AuthorizationFailure(authorization);
        }

        int accepted = await exposureEventStore.AppendAsync(batch, cancellationToken);
        metrics.RecordExposureBatch(accepted);
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
        string prometheus = metrics.RenderPrometheus(manifestRegistry, exposureEventStore, options);
        return Results.Text(prometheus, "text/plain; version=0.0.4", statusCode: StatusCodes.Status200OK);
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
        if (context.Request.Headers.TryGetValue("If-None-Match", out Microsoft.Extensions.Primitives.StringValues values))
        {
            ifNoneMatch = values.ToString();
            return !string.IsNullOrWhiteSpace(ifNoneMatch);
        }

        ifNoneMatch = string.Empty;
        return false;
    }

    private async ValueTask<ExposureBatchRequest> ReadExposureBatchAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
        return ExposureBatchRequest.Parse(document.RootElement, NormalizeEnvironment(null));
    }

    private static IResult AuthorizationFailure(DistributionAuthorizationResult authorization) =>
        authorization.StatusCode == StatusCodes.Status401Unauthorized
            ? Results.Unauthorized()
            : JsonText(authorization.StatusCode, authorization.FailureCode ?? "forbidden");

    private static IResult JsonText(int statusCode, string code) =>
        Results.Text(
            string.Create(CultureInfo.InvariantCulture, $"{{\"error\":\"{code}\"}}"),
            "application/json",
            statusCode: statusCode);
}
