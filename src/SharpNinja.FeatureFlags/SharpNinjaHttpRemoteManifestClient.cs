using System.Net;
using System.Text.Json;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaHttpRemoteManifestClient : ISharpNinjaRemoteManifestClient
{
    private readonly HttpClient httpClient;

    public SharpNinjaHttpRemoteManifestClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async ValueTask<RemoteManifestFetchResult> FetchAsync(
        SharpNinjaFeatureFlagOptions options,
        bool forceRefresh,
        string? currentETag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DistributionBaseUri is null)
        {
            return new RemoteManifestFetchResult(
                Envelope: null,
                ErrorMessage: "Distribution base URI is not configured.",
                NotConfigured: true);
        }

        Uri requestUri = BuildManifestUri(options);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(currentETag) && !forceRefresh)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", currentETag);
        }

        if (forceRefresh)
        {
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
            };
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new RemoteManifestFetchResult(Envelope: null, NotModified: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new RemoteManifestFetchResult(
                Envelope: null,
                ErrorMessage: string.Concat("Remote manifest request failed with status ", (int)response.StatusCode, "."));
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        SignedManifestEnvelope envelope = ReadEnvelope(stream);
        if (envelope.ETag is null && response.Headers.ETag is not null)
        {
            envelope = envelope with { ETag = response.Headers.ETag.Tag };
        }

        return new RemoteManifestFetchResult(envelope);
    }

    private static Uri BuildManifestUri(SharpNinjaFeatureFlagOptions options)
    {
        string relativePath = string.Concat(
            "v1/manifest/",
            Uri.EscapeDataString(options.ProductId),
            "/",
            Uri.EscapeDataString(options.ReleaseId),
            "?environment=",
            Uri.EscapeDataString(options.Environment));

        return new Uri(options.DistributionBaseUri!, relativePath);
    }

    private static SignedManifestEnvelope ReadEnvelope(Stream stream)
    {
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        return new SignedManifestEnvelope(
            ReadRequiredString(root, "manifestJson"),
            ReadRequiredString(root, "signature"),
            ReadRequiredString(root, "signingKeyId"),
            ReadRequiredString(root, "algorithm"))
        {
            ManifestId = ReadOptionalString(root, "manifestId")
                ?? new SignedManifestEnvelope(
                    ReadRequiredString(root, "manifestJson"),
                    ReadRequiredString(root, "signature"),
                    ReadRequiredString(root, "signingKeyId"),
                    ReadRequiredString(root, "algorithm")).ManifestId,
            ETag = ReadOptionalString(root, "eTag"),
            PublishedAt = ReadOptionalDateTimeOffset(root, "publishedAt"),
        };
    }

    private static string ReadRequiredString(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).GetString() ?? string.Empty;

    private static string? ReadOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() : null;

    private static DateTimeOffset? ReadOptionalDateTimeOffset(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDateTimeOffset(out DateTimeOffset result)
            ? result
            : null;
}
