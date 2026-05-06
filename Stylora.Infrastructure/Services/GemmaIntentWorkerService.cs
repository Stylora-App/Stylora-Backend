using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class GemmaIntentWorkerService : IOutfitIntentParser, IAsyncDisposable
{
    private static readonly TimeSpan WorkerHealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly OutfitChatModelSettings _settings;
    private readonly ILogger<GemmaIntentWorkerService> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Process? _process;
    private Uri? _workerBaseUri;
    private Task? _stderrPump;
    private bool _isWorkerReady;
    private bool _disposed;

    public GemmaIntentWorkerService(
        OutfitChatModelSettings settings,
        ILogger<GemmaIntentWorkerService> logger)
    {
        _settings = settings;
        _logger = logger;
        _httpClient.DefaultRequestVersion = HttpVersion.Version11;
        _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        _httpClient.DefaultRequestHeaders.ExpectContinue = false;
    }

    public async Task<OutfitIntentResult> ParseAsync(
        IReadOnlyList<OutfitChatMessageDto> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return new OutfitIntentResult
            {
                IsInScope = true,
                Intent = "clarify_request",
                ParserSource = "empty"
            };
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_workerBaseUri!, "parse"))
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                Content = JsonContent.Create(new
                {
                    messages,
                    modelId = _settings.ModelId,
                    maxNewTokens = _settings.MaxNewTokens,
                    temperature = _settings.Temperature
                })
            };
            request.Headers.ConnectionClose = true;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var workerResponse = await response.Content.ReadFromJsonAsync<IntentParseWorkerResponse>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(workerResponse?.Error ?? "Gemma intent worker rejected the request.");
            }

            if (workerResponse?.Result is null)
            {
                throw new InvalidOperationException("Gemma intent worker returned no parse result.");
            }

            return MergeWithHeuristics(workerResponse.Result, messages);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            or InvalidOperationException
            or TimeoutException
            or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falling back to heuristic outfit intent parsing.");
            return HeuristicParse(messages);
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
                throw new InvalidOperationException($"Gemma python runtime not found at '{pythonPath}'.");
            }

            if (!File.Exists(scriptPath))
            {
                throw new InvalidOperationException($"Gemma worker script not found at '{scriptPath}'.");
            }

            var port = FindAvailablePort();
            _workerBaseUri = new Uri($"http://127.0.0.1:{port}/");
            _isWorkerReady = false;

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments =
                        $"-u \"{scriptPath}\" --port {port} --model-id \"{_settings.ModelId}\" --max-new-tokens {_settings.MaxNewTokens} --temperature {_settings.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start the Gemma intent worker process.");
            }

            _stderrPump = PumpStandardErrorAsync(_process.StandardError);
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
            throw new InvalidOperationException("The Gemma intent worker process was not initialized.");
        }

        var startupDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));
        while (DateTime.UtcNow < startupDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The Gemma intent worker exited during startup with code {_process.ExitCode}.");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_workerBaseUri, "health"))
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
                request.Headers.ConnectionClose = true;

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var health = await response.Content.ReadFromJsonAsync<IntentWorkerHealthResponse>(cancellationToken);
                if (response.IsSuccessStatusCode && health?.Ready == true)
                {
                    _isWorkerReady = true;
                    _logger.LogInformation(
                        "Gemma intent worker is ready in mode {Mode}.",
                        health.Mode ?? "unknown");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Worker is still starting.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Keep polling until the deadline expires.
            }

            await Task.Delay(WorkerHealthPollInterval, cancellationToken);
        }

        await StopWorkerProcessAsync();
        throw new TimeoutException("The Gemma intent worker did not become healthy before the startup timeout elapsed.");
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
                    _logger.LogInformation("Gemma worker: {Line}", line);
                }
            }
        }

        var remaining = pending.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            _logger.LogInformation("Gemma worker: {Line}", remaining);
        }
    }

    private static OutfitIntentResult HeuristicParse(IReadOnlyList<OutfitChatMessageDto> messages)
    {
        var parser = new HeuristicOutfitIntentParser();
        return parser.Parse(messages);
    }

    private static OutfitIntentResult MergeWithHeuristics(
        OutfitIntentResult parsed,
        IReadOnlyList<OutfitChatMessageDto> messages)
    {
        var heuristic = HeuristicParse(messages);

        parsed.IsInScope = parsed.IsInScope || heuristic.IsInScope;
        if (string.Equals(parsed.Intent, "out_of_scope", StringComparison.OrdinalIgnoreCase) && heuristic.IsInScope)
        {
            parsed.Intent = heuristic.Intent;
        }

        parsed.OccasionText = FirstNonEmpty(parsed.OccasionText, heuristic.OccasionText);
        parsed.StyleBucket = FirstNonEmpty(parsed.StyleBucket, heuristic.StyleBucket);
        parsed.Location = FirstNonEmpty(parsed.Location, heuristic.Location);
        parsed.DateContext = FirstNonEmpty(parsed.DateContext, heuristic.DateContext) ?? "today";
        parsed.WeatherSummary = FirstNonEmpty(parsed.WeatherSummary, heuristic.WeatherSummary);
        parsed.WeatherStatus = FirstNonEmpty(parsed.WeatherStatus, heuristic.WeatherStatus);
        parsed.TemperatureC ??= heuristic.TemperatureC;

        foreach (var constraint in heuristic.Constraints)
        {
            if (!parsed.Constraints.Contains(constraint, StringComparer.OrdinalIgnoreCase))
            {
                parsed.Constraints.Add(constraint);
            }
        }

        parsed.ShuffleCount = Math.Max(parsed.ShuffleCount, heuristic.ShuffleCount);
        return parsed;
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback)
    {
        return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
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
        return ((IPEndPoint)listener.LocalEndpoint).Port;
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

    private sealed class IntentWorkerHealthResponse
    {
        public bool Ready { get; set; }
        public string? Mode { get; set; }
    }

    private sealed class IntentParseWorkerResponse
    {
        public OutfitIntentResult? Result { get; set; }
        public string? Error { get; set; }
    }
}
