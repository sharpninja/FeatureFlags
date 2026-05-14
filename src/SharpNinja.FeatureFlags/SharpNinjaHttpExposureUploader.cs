using System.Net.Http.Headers;
using System.Text.Json;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaHttpExposureUploader : ISharpNinjaExposureUploader
{
    private readonly HttpClient httpClient;
    private readonly SharpNinjaFeatureFlagOptions options;

    public SharpNinjaHttpExposureUploader(
        HttpClient httpClient,
        SharpNinjaFeatureFlagOptions options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask UploadAsync(
        IReadOnlyCollection<SharpNinjaExposureEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        Uri endpoint = ResolveEndpoint();
        byte[] payload = WritePayload(events);
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await httpClient
            .PostAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private Uri ResolveEndpoint()
    {
        if (options.ExposureUploadEndpoint is not null)
        {
            return options.ExposureUploadEndpoint;
        }

        if (options.DistributionBaseUri is not null)
        {
            return new Uri(options.DistributionBaseUri, "v1/exposure");
        }

        throw new InvalidOperationException("Exposure upload endpoint is not configured.");
    }

    private byte[] WritePayload(IReadOnlyCollection<SharpNinjaExposureEvent> events)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("productId", options.ProductId);
            writer.WriteString("releaseId", options.ReleaseId);
            writer.WriteString("environment", options.Environment);
            writer.WriteStartArray("events");
            foreach (SharpNinjaExposureEvent exposureEvent in events)
            {
                SharpNinjaExposureJson.WriteEvent(writer, exposureEvent);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
