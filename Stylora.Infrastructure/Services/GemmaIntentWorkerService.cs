using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class GemmaIntentWorkerService : IOutfitIntentParser, IAsyncDisposable
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly OutfitChatModelSettings _settings;
    private readonly ILogger<GemmaIntentWorkerService> _logger;
    private readonly HttpClient _httpClient = new();
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

        try
        {
            var workerBaseUri = new Uri(_settings.WorkerBaseUrl);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(workerBaseUri, "parse"))
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
                throw new InvalidOperationException(workerResponse?.Error ?? "Gemma intent worker rejected the request.");

            if (workerResponse?.Result is null)
                throw new InvalidOperationException("Gemma intent worker returned no parse result.");

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
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        var workerBaseUri = new Uri(_settings.WorkerBaseUrl);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));

        _logger.LogInformation("Waiting for Gemma intent worker at {Url}.", workerBaseUri);

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
                var health = await response.Content.ReadFromJsonAsync<IntentWorkerHealthResponse>(cancellationToken);
                if (response.IsSuccessStatusCode && health?.Ready == true)
                {
                    _logger.LogInformation("Gemma intent worker is ready in mode {Mode}.", health.Mode ?? "unknown");
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(HealthPollInterval, cancellationToken);
        }

        throw new TimeoutException("The Gemma intent worker did not become healthy before the startup timeout elapsed.");
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
            parsed.Intent = heuristic.Intent;

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
                parsed.Constraints.Add(constraint);
        }

        parsed.ShuffleCount = Math.Max(parsed.ShuffleCount, heuristic.ShuffleCount);
        return parsed;
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
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
