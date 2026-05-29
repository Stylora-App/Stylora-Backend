using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class ClipImageEmbeddingWorkerService : IImageEmbeddingService, IAsyncDisposable
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ClothingValidationSettings _settings;
    private readonly ILogger<ClipImageEmbeddingWorkerService> _logger;
    private readonly HttpClient _httpClient = new();
    private bool _disposed;

    public ClipImageEmbeddingWorkerService(
        ClothingValidationSettings settings,
        ILogger<ClipImageEmbeddingWorkerService> logger)
    {
        _settings = settings;
        _logger = logger;
        _httpClient.DefaultRequestVersion = HttpVersion.Version11;
        _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        _httpClient.DefaultRequestHeaders.ExpectContinue = false;
    }

    public async Task<float[]> EmbedImageAsync(string imageBase64, CancellationToken cancellationToken = default)
    {
        var payload = NormalizeImagePayload(imageBase64);
        var workerBaseUri = new Uri(_settings.WorkerBaseUrl);

        _logger.LogInformation("Requesting CLIP image embedding.");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(workerBaseUri, "embed"))
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Content = new StringContent(
                JsonSerializer.Serialize(new { mimeType = payload.MimeType, imageBase64 = payload.Base64 }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.ConnectionClose = true;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var workerResponse = await response.Content.ReadFromJsonAsync<EmbeddingWorkerResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new ArgumentException(workerResponse?.Error ?? "The CLIP embedding worker rejected the image.");

        if (!string.IsNullOrWhiteSpace(workerResponse?.Error))
            throw new ArgumentException(workerResponse.Error);

        if (workerResponse?.Embedding is null || workerResponse.Embedding.Length == 0)
            throw new InvalidOperationException("The CLIP embedding worker did not return an embedding.");

        _logger.LogInformation("Received CLIP image embedding with {Length} dimensions.", workerResponse.Embedding.Length);
        return workerResponse.Embedding;
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var workerBaseUri = new Uri(_settings.WorkerBaseUrl);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));

        _logger.LogInformation("Waiting for CLIP embedding worker at {Url}.", workerBaseUri);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(workerBaseUri, "health"))
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
                request.Headers.ConnectionClose = true;

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var ready = await response.Content.ReadFromJsonAsync<EmbeddingWorkerReadyResponse>(cancellationToken);
                    if (ready?.Ready == true)
                    {
                        _logger.LogInformation(
                            "CLIP embedding worker ready with {Dimensions} dimensions.", ready.Dimensions);
                        return;
                    }
                }
            }
            catch (HttpRequestException) { }
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

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private sealed record ImagePayload(string MimeType, string Base64);

    private sealed class EmbeddingWorkerReadyResponse
    {
        public bool Ready { get; set; }
        public int Dimensions { get; set; }
    }

    private sealed class EmbeddingWorkerResponse
    {
        public float[]? Embedding { get; set; }
        public string? Error { get; set; }
    }
}
