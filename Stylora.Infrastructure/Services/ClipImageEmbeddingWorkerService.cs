using Microsoft.Extensions.Logging;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Infrastructure.Generated.Clip;

namespace Stylora.Infrastructure.Services;

public sealed class ClipImageEmbeddingWorkerService : IImageEmbeddingService
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ClothingValidationSettings _settings;
    private readonly IClipWorkerClient _client;
    private readonly ILogger<ClipImageEmbeddingWorkerService> _logger;

    public ClipImageEmbeddingWorkerService(
        ClothingValidationSettings settings,
        IClipWorkerClient client,
        ILogger<ClipImageEmbeddingWorkerService> logger)
    {
        _settings = settings;
        _client = client;
        _logger = logger;
    }

    public async Task<float[]> EmbedImageAsync(string imageBase64, CancellationToken cancellationToken = default)
    {
        var payload = NormalizeImagePayload(imageBase64);
        _logger.LogInformation("Requesting CLIP image embedding.");

        EmbedResponse response;
        try
        {
            response = await _client.EmbedAsync(
                new EmbedRequest { MimeType = payload.MimeType, ImageBase64 = payload.Base64 },
                cancellationToken);
        }
        catch (ApiException<EmbedResponse> ex) when (ex.StatusCode == 400)
        {
            throw new ArgumentException(ex.Result.Error ?? "The CLIP embedding worker rejected the image.");
        }

        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new ArgumentException(response.Error);

        if (response.Embedding is null || response.Embedding.Count == 0)
            throw new InvalidOperationException("The CLIP embedding worker did not return an embedding.");

        _logger.LogInformation("Received CLIP image embedding with {Length} dimensions.", response.Embedding.Count);
        return [.. response.Embedding];
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));
        _logger.LogInformation("Waiting for CLIP embedding worker at {Url}.", _settings.WorkerBaseUrl);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var health = await _client.GetHealthAsync(cancellationToken);
                if (health.Ready)
                {
                    _logger.LogInformation("CLIP embedding worker ready with {Dimensions} dimensions.", health.Dimensions);
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (ApiException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(HealthPollInterval, cancellationToken);
        }

        throw new TimeoutException("The CLIP embedding worker did not become healthy before the startup timeout elapsed.");
    }

    private static ImagePayload NormalizeImagePayload(string imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
            throw new ArgumentException("Image data is required.");

        var mimeType = "image/jpeg";
        var base64 = imageBase64.Trim();

        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = base64.IndexOf(',');
            if (separatorIndex <= 5)
                throw new ArgumentException("Invalid image payload format.");

            var metadata = base64[..separatorIndex];
            var parts = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
                mimeType = parts[0][5..];

            base64 = base64[(separatorIndex + 1)..];
        }

        try
        {
            Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("The uploaded image is not valid base64 data.");
        }

        return new ImagePayload(mimeType, base64);
    }

    private sealed record ImagePayload(string MimeType, string Base64);
}
