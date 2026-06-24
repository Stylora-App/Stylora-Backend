using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Infrastructure.Generated.Gemma;

namespace Stylora.Infrastructure.Services;

public sealed class GemmaIntentWorkerService : IOutfitIntentParser
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly OutfitChatModelSettings _settings;
    private readonly IGemmaWorkerClient _client;
    private readonly ILogger<GemmaIntentWorkerService> _logger;

    public GemmaIntentWorkerService(
        OutfitChatModelSettings settings,
        IGemmaWorkerClient client,
        ILogger<GemmaIntentWorkerService> logger)
    {
        _settings = settings;
        _client = client;
        _logger = logger;
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
            var request = new ParseRequest
            {
                Messages = messages
                    .Select(m => new ChatMessage
                    {
                        Role = Enum.TryParse<ChatMessageRole>(m.Role, ignoreCase: true, out var role)
                            ? role
                            : ChatMessageRole.User,
                        Content = m.Content
                    })
                    .ToList(),
                ModelId = _settings.ModelId,
                MaxNewTokens = _settings.MaxNewTokens,
                Temperature = (float)_settings.Temperature
            };

            var workerResponse = await _client.ParseAsync(request, cancellationToken);

            if (workerResponse.Result is null)
                throw new InvalidOperationException("Gemma intent worker returned no parse result.");

            return MergeWithHeuristics(ToOutfitIntentResult(workerResponse.Result), messages);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            or ApiException
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
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(30, _settings.WorkerStartupTimeoutSeconds));
        _logger.LogInformation("Waiting for Gemma intent worker at {Url}.", _settings.WorkerBaseUrl);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var health = await _client.GetHealthAsync(cancellationToken);
                if (health.Ready)
                {
                    _logger.LogInformation("Gemma intent worker is ready in mode {Mode}.", health.Mode);
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (ApiException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(HealthPollInterval, cancellationToken);
        }

        throw new TimeoutException("The Gemma intent worker did not become healthy before the startup timeout elapsed.");
    }

    private static OutfitIntentResult ToOutfitIntentResult(IntentResult r) => new()
    {
        Intent = GetEnumMemberValue(r.Intent),
        IsInScope = r.Is_in_scope,
        OccasionText = r.Occasion_text,
        StyleBucket = GetEnumMemberValue(r.Style_bucket),
        Location = r.Location,
        DateContext = r.Date_context,
        WeatherSummary = r.Weather_summary,
        WeatherStatus = GetEnumMemberValue(r.Weather_status),
        TemperatureC = (double?)r.Temperature_c,
        Constraints = r.Constraints?.ToList() ?? [],
        ShuffleCount = r.Shuffle_count,
        ParserSource = GetEnumMemberValue(r.Parser_source)
    };

    private static string GetEnumMemberValue<T>(T value) where T : struct, Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<EnumMemberAttribute>();
        return attr?.Value ?? value.ToString().ToLowerInvariant();
    }

    private static string? GetEnumMemberValue<T>(T? value) where T : struct, Enum
        => value.HasValue ? GetEnumMemberValue(value.Value) : null;

    private static OutfitIntentResult HeuristicParse(IReadOnlyList<OutfitChatMessageDto> messages)
    {
        var parser = new HeuristicOutfitIntentParser();
        return parser.Parse(messages);
    }

    private static OutfitIntentResult MergeWithHeuristics(OutfitIntentResult parsed, IReadOnlyList<OutfitChatMessageDto> messages)
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
}
