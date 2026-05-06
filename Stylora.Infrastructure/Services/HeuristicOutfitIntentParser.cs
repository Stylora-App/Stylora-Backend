using System.Text.RegularExpressions;
using Stylora.Application.DTOs;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

internal sealed class HeuristicOutfitIntentParser
{
    private static readonly HashSet<string> LocationStopWords =
    [
        "for",
        "a",
        "an",
        "the",
        "to",
        "with",
        "on",
        "at",
        "and",
        "but",
        "today",
        "tomorrow",
        "tonight",
        "this",
        "weekend",
        "weather",
        "casual",
        "office",
        "sport",
        "sporty",
        "elegant",
        "formal",
        "bohemian",
        "streetwear",
        "drink",
        "outfit",
        "look",
        "go",
        "going",
        "need",
        "want"
    ];
    private static readonly Dictionary<string, string> StyleByOccasion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["walk"] = "casual",
        ["coffee"] = "casual",
        ["brunch"] = "casual",
        ["errands"] = "casual",
        ["weekend"] = "casual",
        ["trip"] = "casual",
        ["travel"] = "casual",
        ["vacation"] = "casual",
        ["holiday"] = "casual",
        ["flight"] = "casual",
        ["work"] = "office",
        ["office"] = "office",
        ["meeting"] = "office",
        ["interview"] = "formal",
        ["conference"] = "formal",
        ["theatre"] = "elegant",
        ["theater"] = "elegant",
        ["film"] = "elegant",
        ["movie"] = "elegant",
        ["cinema"] = "elegant",
        ["party"] = "elegant",
        ["dinner"] = "elegant",
        ["date"] = "elegant",
        ["wedding"] = "formal",
        ["ceremony"] = "formal",
        ["gym"] = "sport",
        ["workout"] = "sport",
        ["training"] = "sport",
        ["run"] = "sport"
    };

    private static readonly string[] AllowedStyles = ["casual", "office", "sport", "elegant", "bohemian", "streetwear", "formal"];
    private static readonly string[] ScopeKeywords = ["outfit", "wear", "wearing", "look", "dress", "style", "wardrobe", "shuffle", "another option"];
    private static readonly string[] ShuffleKeywords = ["shuffle", "another option", "another look", "different option", "different outfit"];
    private static readonly string[] WeatherStatusKeywords = ["sunny", "cloudy", "rainy", "windy", "snowy", "stormy"];
    private static readonly string[] ConstraintKeywords = ["warmer", "cooler", "more casual", "less casual", "more formal", "less formal", "layered", "minimal", "sportier"];
    private static readonly string[] DateKeywords = ["today", "tonight", "tomorrow", "weekend", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];

    public OutfitIntentResult Parse(IReadOnlyList<OutfitChatMessageDto> messages)
    {
        var userMessages = messages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(message => message.Content.Trim())
            .Where(message => message.Length > 0)
            .ToList();

        if (userMessages.Count == 0)
        {
            return new OutfitIntentResult
            {
                Intent = "clarify_request",
                ParserSource = "heuristic"
            };
        }

        var fullConversation = string.Join(' ', userMessages);
        if (!HasOutfitSignals(fullConversation))
        {
            return new OutfitIntentResult
            {
                IsInScope = false,
                Intent = "out_of_scope",
                ParserSource = "heuristic"
            };
        }

        var intent = new OutfitIntentResult
        {
            IsInScope = true,
            ParserSource = "heuristic"
        };

        foreach (var message in userMessages)
        {
            var lowered = message.ToLowerInvariant();

            if (ShuffleKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                intent.ShuffleCount++;
            }

            foreach (var style in AllowedStyles)
            {
                if (lowered.Contains(style, StringComparison.OrdinalIgnoreCase))
                {
                    intent.StyleBucket = style;
                    intent.OccasionText ??= style;
                }
            }

            foreach (var occasion in StyleByOccasion)
            {
                if (!lowered.Contains(occasion.Key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                intent.OccasionText ??= occasion.Key;
                intent.StyleBucket ??= occasion.Value;
            }

            foreach (var keyword in ConstraintKeywords)
            {
                if (lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    intent.Constraints.Add(keyword);
                }
            }

            var explicitWeather = ExtractWeather(message);
            if (explicitWeather is not null)
            {
                intent.WeatherSummary = explicitWeather.Summary;
                intent.WeatherStatus = explicitWeather.Status;
                intent.TemperatureC = explicitWeather.TemperatureC;
            }

            var location = ExtractLocation(message);
            if (!string.IsNullOrWhiteSpace(location))
            {
                intent.Location = location;
            }

            var dateContext = DateKeywords.FirstOrDefault(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(dateContext))
            {
                intent.DateContext = dateContext;
            }
        }

        intent.DateContext ??= "today";
        intent.Intent = intent.ShuffleCount > 0 ? "shuffle_outfit" : "generate_outfit";
        return intent;
    }

    private static bool HasOutfitSignals(string message)
    {
        return ScopeKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || AllowedStyles.Any(style => message.Contains(style, StringComparison.OrdinalIgnoreCase))
            || StyleByOccasion.Keys.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || WeatherStatusKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || message.Contains("cold", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cool", StringComparison.OrdinalIgnoreCase)
            || message.Contains("warm", StringComparison.OrdinalIgnoreCase)
            || message.Contains("hot", StringComparison.OrdinalIgnoreCase)
            || DateKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            || !string.IsNullOrWhiteSpace(ExtractLocation(message));
    }

    private static WeatherExtraction? ExtractWeather(string message)
    {
        var lowered = message.ToLowerInvariant();
        var status = WeatherStatusKeywords.FirstOrDefault(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        string? thermalBand = null;
        if (lowered.Contains("freezing", StringComparison.OrdinalIgnoreCase))
        {
            thermalBand = "freezing";
        }
        else if (lowered.Contains("cold", StringComparison.OrdinalIgnoreCase) || lowered.Contains("chilly", StringComparison.OrdinalIgnoreCase))
        {
            thermalBand = "cold";
        }
        else if (lowered.Contains("cool", StringComparison.OrdinalIgnoreCase))
        {
            thermalBand = "cool";
        }
        else if (lowered.Contains("warm", StringComparison.OrdinalIgnoreCase))
        {
            thermalBand = "warm";
        }
        else if (lowered.Contains("hot", StringComparison.OrdinalIgnoreCase))
        {
            thermalBand = "hot";
        }

        double? temperatureC = null;
        var temperatureMatch = Regex.Match(lowered, @"(-?\d{1,2}(?:\.\d+)?)\s*(?:°?\s*c|degrees?)");
        if (temperatureMatch.Success && double.TryParse(temperatureMatch.Groups[1].Value, out var parsedTemperature))
        {
            temperatureC = parsedTemperature;
            thermalBand = GetThermalBand(parsedTemperature);
        }

        if (status is null && thermalBand is null)
        {
            return null;
        }

        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(status))
        {
            summaryParts.Add(status);
        }

        if (temperatureC is not null)
        {
            summaryParts.Add($"{temperatureC:0.#}C");
        }
        else if (!string.IsNullOrWhiteSpace(thermalBand))
        {
            summaryParts.Add(thermalBand);
        }

        return new WeatherExtraction(
            status ?? thermalBand ?? "mild",
            temperatureC,
            string.Join(", ", summaryParts));
    }

    private static string? ExtractLocation(string message)
    {
        var contextualCandidate = ExtractLocationAfterPreposition(message);
        if (!string.IsNullOrWhiteSpace(contextualCandidate))
        {
            return NormalizeLocationCandidate(contextualCandidate);
        }

        foreach (var segment in SplitMessageSegments(message))
        {
            var standaloneMatch = Regex.Match(
                segment.Trim(),
                @"^([A-Z][a-z]+(?:[\s-][A-Z][a-z]+){0,3})(?:\s+(?:today|tomorrow|tonight|this weekend|weekend))?$",
                RegexOptions.CultureInvariant);

            if (standaloneMatch.Success)
            {
                return NormalizeLocationCandidate(standaloneMatch.Groups[1].Value);
            }
        }

        return null;
    }

    private static string? NormalizeLocationCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        normalized = Regex.Replace(
            normalized,
            @"\s+(today|tomorrow|tonight|this weekend|weekend)$",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return normalized.Trim();
    }

    private static string? ExtractLocationAfterPreposition(string message)
    {
        return ExtractLocationAfterKeyword(message, "in")
            ?? ExtractLocationAfterKeyword(message, "to")
            ?? ExtractLocationAfterKeyword(message, "for");
    }

    private static string? ExtractLocationAfterKeyword(string message, string keyword)
    {
        var matches = Regex.Matches(
            message,
            $@"\b{keyword}\b\s+(.+?)(?=(?:\b(?:in|to|for)\b\s+)|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var tail = matches[i].Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(tail))
            {
                continue;
            }

            var words = tail
                .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var candidateWords = new List<string>();
            foreach (var rawWord in words)
            {
                var cleanedWord = rawWord.Trim(',', '.', '!', '?', ';', ':');
                if (string.IsNullOrWhiteSpace(cleanedWord))
                {
                    continue;
                }

                if (LocationStopWords.Contains(cleanedWord.ToLowerInvariant()))
                {
                    break;
                }

                if (!Regex.IsMatch(cleanedWord, @"^[A-Za-z-]+$", RegexOptions.CultureInvariant))
                {
                    break;
                }

                candidateWords.Add(cleanedWord);
                if (candidateWords.Count == 4)
                {
                    break;
                }
            }

            if (candidateWords.Count > 0)
            {
                return string.Join(' ', candidateWords);
            }
        }

        return null;
    }

    private static IEnumerable<string> SplitMessageSegments(string message)
    {
        return message.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetThermalBand(double temperatureC)
    {
        return temperatureC switch
        {
            <= 4 => "freezing",
            <= 11 => "cold",
            <= 18 => "cool",
            <= 26 => "warm",
            _ => "hot"
        };
    }

    private sealed record WeatherExtraction(string Status, double? TemperatureC, string Summary);
}
