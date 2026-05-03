using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class ClipImageEmbeddingWorkerService : IImageEmbeddingService, IAsyncDisposable
{
    private static readonly TimeSpan WorkerHealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ClothingValidationSettings _settings;
    private readonly ILogger<ClipImageEmbeddingWorkerService> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Process? _process;
    private Uri? _workerBaseUri;
    private Task? _stderrPump;
    private bool _isWorkerReady;
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

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            _logger.LogInformation("Requesting CLIP image embedding.");

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_workerBaseUri!, "embed"))
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        mimeType = payload.MimeType,
                        imageBase64 = payload.Base64
                    }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.ConnectionClose = true;

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var workerResponse = await response.Content.ReadFromJsonAsync<EmbeddingWorkerResponse>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ArgumentException(workerResponse?.Error ?? "The CLIP embedding worker rejected the image.");
            }

            if (!string.IsNullOrWhiteSpace(workerResponse?.Error))
            {
                throw new ArgumentException(workerResponse.Error);
            }

            if (workerResponse?.Embedding is null || workerResponse.Embedding.Length == 0)
            {
                throw new InvalidOperationException("The CLIP embedding worker did not return an embedding.");
            }

            _logger.LogInformation("Received CLIP image embedding with {Length} dimensions.", workerResponse.Embedding.Length);
            return workerResponse.Embedding;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_isWorkerReady && _process is { HasExited: false } && _workerBaseUri is not null)
        {
            return;
        }

        if (_process is not { HasExited: false } || _workerBaseUri is null)
        {
            var pythonPath = ResolvePath(_settings.PythonExecutablePath);
            var scriptPath = ResolvePath(_settings.WorkerScriptPath);

            if (!File.Exists(pythonPath))
            {
                throw new InvalidOperationException($"CLIP python runtime not found at '{pythonPath}'.");
            }

            if (!File.Exists(scriptPath))
            {
                throw new InvalidOperationException($"CLIP worker script not found at '{scriptPath}'.");
            }

            var port = FindAvailablePort();
            _workerBaseUri = new Uri($"http://127.0.0.1:{port}/");
            _isWorkerReady = false;

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"-u \"{scriptPath}\" --model-id \"{_settings.ModelId}\" --port {port}",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start the CLIP embedding worker process.");
            }

            _stderrPump = PumpStandardErrorAsync(_process.StandardError);
            _logger.LogInformation("Waiting for CLIP embedding worker health check on port {Port}.", port);
        }

        try
        {
            await WaitForWorkerReadyAsync(cancellationToken);
        }
        catch
        {
            _isWorkerReady = false;
            throw;
        }
    }

    private async Task WaitForWorkerReadyAsync(CancellationToken cancellationToken)
    {
        if (_process is null || _workerBaseUri is null)
        {
            throw new InvalidOperationException("The CLIP embedding worker process was not initialized.");
        }

        var process = _process;
        var workerBaseUri = _workerBaseUri;
        var startupDeadline = DateTime.UtcNow + GetWorkerStartupTimeout();
        while (DateTime.UtcNow < startupDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The CLIP embedding worker exited during startup with code {process.ExitCode}.");
            }

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
                        _isWorkerReady = true;
                        _logger.LogInformation(
                            "CLIP embedding worker initialized successfully with {Dimensions} dimensions.",
                            ready.Dimensions);
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Worker is still starting up.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Keep polling until the overall startup timeout expires.
            }

            await Task.Delay(WorkerHealthPollInterval, cancellationToken);
        }

        await StopWorkerProcessAsync();
        throw new TimeoutException("The CLIP embedding worker did not become healthy before the startup timeout elapsed.");
    }

    private TimeSpan GetWorkerStartupTimeout()
    {
        return TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));
    }

    private async Task PumpStandardErrorAsync(StreamReader stderr)
    {
        var buffer = new char[2048];
        var pending = new StringBuilder();

        while (true)
        {
            var read = await stderr.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            pending.Append(buffer, 0, read);

            while (true)
            {
                var newlineIndex = pending.ToString().IndexOfAny(['\r', '\n']);
                if (newlineIndex < 0)
                {
                    break;
                }

                var line = pending.ToString(0, newlineIndex).Trim();
                pending.Remove(0, newlineIndex + 1);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogInformation("CLIP worker: {Line}", line);
                }
            }
        }

        var remaining = pending.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            _logger.LogInformation("CLIP worker: {Line}", remaining);
        }
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        return endpoint.Port;
    }

    private static ImagePayload NormalizeImagePayload(string imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            throw new ArgumentException("Image data is required.");
        }

        var mimeType = "image/jpeg";
        var base64 = imageBase64.Trim();

        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = base64.IndexOf(',');
            if (separatorIndex <= 5)
            {
                throw new ArgumentException("Invalid image payload format.");
            }

            var metadata = base64[..separatorIndex];
            var parts = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                mimeType = parts[0][5..];
            }

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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();

        await StopWorkerProcessAsync();

        _process?.Dispose();
        _httpClient.Dispose();
    }

    private async Task StopWorkerProcessAsync()
    {
        _isWorkerReady = false;

        if (_process is { HasExited: false } && _workerBaseUri is not null)
        {
            try
            {
                await _httpClient.PostAsync(new Uri(_workerBaseUri, "shutdown"), null);
            }
            catch
            {
                // Best effort.
            }

            if (!_process.WaitForExit(2000))
            {
                _process.Kill(entireProcessTree: true);
            }
        }

        _workerBaseUri = null;
        _process?.Dispose();
        _process = null;
        _stderrPump = null;
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
