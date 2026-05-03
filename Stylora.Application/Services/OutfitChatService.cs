using Stylora.Application.ClothingTags;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Application.Services;

public class OutfitChatService : IOutfitChatService
{
    private static readonly string[] ShuffleKeywords = ["shuffle", "another option", "another look", "different option", "different outfit"];
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
    private static readonly string[] ScopeKeywords = ["outfit", "wear", "wearing", "look", "dress", "style", "wardrobe", "shuffle", "another option"];
    private static readonly string[] WeatherStatusKeywords = ["sunny", "cloudy", "rainy", "windy", "snowy", "stormy"];
    private static readonly string[] DateKeywords = ["today", "tonight", "tomorrow", "weekend", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
    private static readonly Dictionary<string, string> StyleByOccasion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["walk"] = "casual",
        ["coffee"] = "casual",
        ["brunch"] = "casual",
        ["errands"] = "casual",
        ["weekend"] = "casual",
        ["work"] = "office",
        ["office"] = "office",
        ["meeting"] = "office",
        ["interview"] = "formal",
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

    private static readonly HashSet<string> AllowedStyles =
    [
        "casual",
        "office",
        "sport",
        "elegant",
        "bohemian",
        "streetwear",
        "formal"
    ];

    private static readonly HashSet<string> NeutralColors =
    [
        "black",
        "white",
        "gray",
        "blue",
        "brown"
    ];

    private readonly IWardrobeService _wardrobeService;
    private readonly IUserService _userService;
    private readonly IOutfitIntentParser _intentParser;
    private readonly IWeatherService _weatherService;

    public OutfitChatService(
        IWardrobeService wardrobeService,
        IUserService userService,
        IOutfitIntentParser intentParser,
        IWeatherService weatherService)
    {
        _wardrobeService = wardrobeService;
        _userService = userService;
        _intentParser = intentParser;
        _weatherService = weatherService;
    }

    public async Task<OutfitChatResponse> ProcessAsync(string userId, OutfitChatRequest request)
    {
        var messages = request.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .ToList();

        var userMessages = messages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(message => message.Content.Trim())
            .Where(message => message.Length > 0)
            .ToList();

        if (userMessages.Count == 0)
        {
            return BuildFollowUpResponse(
                ["occasion", "weather"],
                "Tell me what the outfit is for and what weather I should dress for.",
                ["Build me a rainy work outfit", "Plan a sunny weekend look", "Create an elegant dinner outfit"]);
        }

        var intent = EnrichIntentFromConversation(messages, await _intentParser.ParseAsync(messages));
        if (!intent.IsInScope || string.Equals(intent.Intent, "out_of_scope", StringComparison.OrdinalIgnoreCase))
        {
            return new OutfitChatResponse
            {
                Status = "out_of_scope",
                AssistantMessage = "I can help only with outfit suggestions based on your wardrobe, palette, and weather."
            };
        }

        var style = ResolveStyle(intent);
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(style))
        {
            missingFields.Add("occasion");
        }

        var weather = await _weatherService.ResolveAsync(intent);
        if (weather is null)
        {
            missingFields.Add("weather");
        }

        if (missingFields.Count > 0)
        {
            return BuildFollowUpForContext(missingFields, intent);
        }

        var wardrobeItems = (await _wardrobeService.GetAllItemsAsync(userId)).ToList();
        var userProfile = await _userService.GetProfileAsync(userId);

        if (wardrobeItems.Count == 0)
        {
            return new OutfitChatResponse
            {
                Status = "not_enough_pieces",
                AssistantMessage = "Your wardrobe is empty, so I cannot build an outfit yet.",
                MissingRoles = ["top", "bottom", "shoes"]
            };
        }

        var gender = InferGender(wardrobeItems);
        var missingRoles = DetermineMissingRoles(wardrobeItems, gender);
        if (missingRoles.Count > 0)
        {
            return new OutfitChatResponse
            {
                Status = "not_enough_pieces",
                AssistantMessage = "I need a few more wardrobe pieces before I can build a complete outfit.",
                MissingRoles = missingRoles
            };
        }

        var occasion = intent.OccasionText ?? style!;
        var paletteProfile = BuildPaletteProfile(userProfile.Palette);
        var candidates = BuildCandidates(
            wardrobeItems,
            style!,
            occasion,
            weather!,
            gender,
            paletteProfile,
            intent.Constraints);

        if (candidates.Count == 0)
        {
            return new OutfitChatResponse
            {
                Status = "not_enough_pieces",
                AssistantMessage = "I could not assemble a full outfit from the current wardrobe mix.",
                MissingRoles = DetermineMissingRoles(wardrobeItems, gender)
            };
        }

        var candidateIndex = Math.Min(intent.ShuffleCount, candidates.Count - 1);
        var selectedCandidate = candidates[candidateIndex];

        return new OutfitChatResponse
        {
            Status = "success",
            AssistantMessage = candidateIndex == 0
                ? $"I put together a {style} look for {occasion} in {weather!.Summary} weather."
                : $"Here is another {style} option for {occasion} in {weather!.Summary} weather.",
            HasMoreOutfits = candidates.Count > candidateIndex + 1,
            Outfit = new OutfitBoardDto
            {
                Occasion = occasion,
                Style = style!,
                WeatherSummary = weather!.Summary,
                Gender = gender,
                Summary = selectedCandidate.Summary,
                Palette = userProfile.Palette ?? [],
                Items = selectedCandidate.Items.Select(MapBoardItem).ToList()
            }
        };
    }

    private static OutfitChatResponse BuildFollowUpForContext(List<string> missingFields, OutfitIntentResult intent)
    {
        if (missingFields.SequenceEqual(["occasion", "weather"]))
        {
            return BuildFollowUpResponse(
                missingFields,
                "Tell me the occasion or vibe and either the weather or the city I should check.",
                ["Work outfit for a rainy day", "Casual sunny weekend look", "Elegant dinner outfit for Bucharest tomorrow"]);
        }

        if (missingFields.Contains("occasion"))
        {
            return BuildFollowUpResponse(
                missingFields,
                "What should the outfit feel like: casual, office, sporty, elegant, or formal?",
                ["Casual", "Office", "Elegant"]);
        }

        var weatherPrompt = string.IsNullOrWhiteSpace(intent.Location)
            ? "What weather should I dress for? You can describe it directly or tell me the city."
            : "Tell me if this is for today or tomorrow, or describe the weather directly.";

        return BuildFollowUpResponse(
            missingFields,
            weatherPrompt,
            ["Rainy and cool", "Sunny and warm", "Bucharest tomorrow"]);
    }

    private static OutfitChatResponse BuildFollowUpResponse(List<string> missingFields, string message, List<string> suggestions)
    {
        return new OutfitChatResponse
        {
            Status = "follow_up",
            AssistantMessage = message,
            MissingFields = missingFields,
            SuggestedReplies = suggestions
        };
    }

    private static OutfitIntentResult EnrichIntentFromConversation(
        IReadOnlyList<OutfitChatMessageDto> messages,
        OutfitIntentResult parsedIntent)
    {
        var intent = CloneIntent(parsedIntent);
        var userMessages = messages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(message => message.Content.Trim())
            .Where(message => message.Length > 0)
            .ToList();

        if (userMessages.Count == 0)
        {
            return intent;
        }

        var fullConversation = string.Join(' ', userMessages);
        if (ScopeKeywords.Any(keyword => fullConversation.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            intent.IsInScope = true;
            if (string.Equals(intent.Intent, "out_of_scope", StringComparison.OrdinalIgnoreCase))
            {
                intent.Intent = "generate_outfit";
            }
        }

        foreach (var userMessage in userMessages)
        {
            FillOccasionAndStyle(userMessage, intent);
            FillWeather(userMessage, intent);
            FillDateContext(userMessage, intent);
            FillLocation(userMessage, intent);
        }

        intent.ShuffleCount = userMessages.Count(IsShuffleMessage);
        if (intent.ShuffleCount > 0)
        {
            intent.Intent = "shuffle_outfit";
        }

        ApplyLatestReplyContext(messages, intent);
        return intent;
    }

    private static OutfitIntentResult CloneIntent(OutfitIntentResult source)
    {
        return new OutfitIntentResult
        {
            Intent = source.Intent,
            IsInScope = source.IsInScope,
            OccasionText = source.OccasionText,
            StyleBucket = source.StyleBucket,
            Location = source.Location,
            DateContext = source.DateContext,
            WeatherSummary = source.WeatherSummary,
            WeatherStatus = source.WeatherStatus,
            TemperatureC = source.TemperatureC,
            Constraints = [.. source.Constraints],
            ShuffleCount = source.ShuffleCount,
            ParserSource = source.ParserSource
        };
    }

    private static void ApplyLatestReplyContext(IReadOnlyList<OutfitChatMessageDto> messages, OutfitIntentResult intent)
    {
        var latestUser = messages.LastOrDefault(message =>
            string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(message.Content))?.Content.Trim();

        if (string.IsNullOrWhiteSpace(latestUser))
        {
            return;
        }

        var previousAssistant = messages
            .Take(Math.Max(0, messages.Count - 1))
            .LastOrDefault(message =>
                string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(message.Content))?.Content.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(previousAssistant))
        {
            return;
        }

        if (NeedsWeatherContext(previousAssistant) && string.IsNullOrWhiteSpace(intent.Location))
        {
            var standaloneLocation = ExtractStandaloneLocation(latestUser);
            if (!string.IsNullOrWhiteSpace(standaloneLocation))
            {
                intent.Location = standaloneLocation;
            }
        }

        if (NeedsWeatherContext(previousAssistant) && string.IsNullOrWhiteSpace(intent.DateContext))
        {
            FillDateContext(latestUser, intent);
        }

        if (NeedsOccasionContext(previousAssistant))
        {
            FillOccasionAndStyle(latestUser, intent);
        }
    }

    private static bool NeedsWeatherContext(string assistantMessage)
    {
        return assistantMessage.Contains("weather", StringComparison.OrdinalIgnoreCase)
            || assistantMessage.Contains("city", StringComparison.OrdinalIgnoreCase)
            || assistantMessage.Contains("dress for", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsOccasionContext(string assistantMessage)
    {
        return assistantMessage.Contains("feel like", StringComparison.OrdinalIgnoreCase)
            || assistantMessage.Contains("occasion", StringComparison.OrdinalIgnoreCase)
            || assistantMessage.Contains("vibe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShuffleMessage(string message)
    {
        return ShuffleKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void FillOccasionAndStyle(string message, OutfitIntentResult intent)
    {
        foreach (var style in AllowedStyles)
        {
            if (message.Contains(style, StringComparison.OrdinalIgnoreCase))
            {
                intent.StyleBucket ??= style;
                intent.OccasionText ??= style;
            }
        }

        foreach (var occasion in StyleByOccasion)
        {
            if (!message.Contains(occasion.Key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            intent.OccasionText ??= occasion.Key;
            intent.StyleBucket ??= occasion.Value;
        }
    }

    private static void FillWeather(string message, OutfitIntentResult intent)
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
        var temperatureMatch = System.Text.RegularExpressions.Regex.Match(lowered, @"(-?\d{1,2}(?:\.\d+)?)\s*(?:°?\s*c|degrees?)");
        if (temperatureMatch.Success && double.TryParse(temperatureMatch.Groups[1].Value, out var parsedTemperature))
        {
            temperatureC = parsedTemperature;
            thermalBand = GetThermalBand(parsedTemperature);
        }

        if (status is null && thermalBand is null)
        {
            return;
        }

        intent.WeatherStatus ??= status ?? thermalBand;
        intent.TemperatureC ??= temperatureC;

        if (string.IsNullOrWhiteSpace(intent.WeatherSummary))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                parts.Add(status);
            }

            if (temperatureC is not null)
            {
                parts.Add($"{temperatureC:0.#}C");
            }
            else if (!string.IsNullOrWhiteSpace(thermalBand))
            {
                parts.Add(thermalBand);
            }

            intent.WeatherSummary = string.Join(", ", parts);
        }
    }

    private static void FillDateContext(string message, OutfitIntentResult intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.DateContext))
        {
            return;
        }

        var match = DateKeywords.FirstOrDefault(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(match))
        {
            intent.DateContext = match;
        }
    }

    private static void FillLocation(string message, OutfitIntentResult intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.Location))
        {
            return;
        }

        intent.Location = ExtractLocationFromMessage(message);
    }

    private static string? ExtractStandaloneLocation(string message)
    {
        foreach (var segment in SplitMessageSegments(message))
        {
            var trimmed = segment.Trim();
            if (trimmed.Length < 2
                || trimmed.Length > 40
                || trimmed.Any(char.IsDigit))
            {
                continue;
            }

            if (AllowedStyles.Contains(trimmed.ToLowerInvariant())
                || WeatherStatusKeywords.Any(keyword => trimmed.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                || DateKeywords.Any(keyword => trimmed.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                || trimmed.Contains("outfit", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("look", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z][A-Za-z\s-]{1,39}$"))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string? ExtractLocationFromMessage(string message)
    {
        var contextualCandidate = ExtractLocationAfterPreposition(message);
        if (!string.IsNullOrWhiteSpace(contextualCandidate))
        {
            return NormalizeLocationCandidate(contextualCandidate);
        }

        foreach (var segment in SplitMessageSegments(message))
        {
            var standaloneMatch = System.Text.RegularExpressions.Regex.Match(
                segment.Trim(),
                @"^([A-Z][a-z]+(?:[\s-][A-Z][a-z]+){0,3})(?:\s+(?:today|tomorrow|tonight|this weekend|weekend))?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

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
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\s+(today|tomorrow|tonight|this weekend|weekend)$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return normalized.Trim();
    }

    private static string? ExtractLocationAfterPreposition(string message)
    {
        return ExtractLocationAfterKeyword(message, "in")
            ?? ExtractLocationAfterKeyword(message, "for");
    }

    private static string? ExtractLocationAfterKeyword(string message, string keyword)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            message,
            $@"\b{keyword}\b\s+(.+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        var tail = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(tail))
        {
            return null;
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

            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanedWord, @"^[A-Za-z-]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                break;
            }

            candidateWords.Add(cleanedWord);
            if (candidateWords.Count == 4)
            {
                break;
            }
        }

        return candidateWords.Count == 0
            ? null
            : string.Join(' ', candidateWords);
    }

    private static IEnumerable<string> SplitMessageSegments(string message)
    {
        return message.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? ResolveStyle(OutfitIntentResult intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.StyleBucket) && AllowedStyles.Contains(intent.StyleBucket.Trim().ToLowerInvariant()))
        {
            return intent.StyleBucket.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(intent.OccasionText))
        {
            return null;
        }

        foreach (var occasion in StyleByOccasion)
        {
            if (intent.OccasionText.Contains(occasion.Key, StringComparison.OrdinalIgnoreCase))
            {
                return occasion.Value;
            }
        }

        return null;
    }

    private static string InferGender(IReadOnlyCollection<WardrobeItemDto> items)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["women"] = 0,
            ["men"] = 0,
            ["unisex"] = 0
        };

        foreach (var item in items)
        {
            var audience = item.AudienceTag?.Trim().ToLowerInvariant();
            if (audience is not null && counts.ContainsKey(audience))
            {
                counts[audience]++;
            }
        }

        return counts.OrderByDescending(entry => entry.Value).First().Value == 0
            ? "unisex"
            : counts.OrderByDescending(entry => entry.Value).First().Key;
    }

    private static List<string> DetermineMissingRoles(IReadOnlyCollection<WardrobeItemDto> items, string gender)
    {
        var grouped = items
            .GroupBy(item => ClothingTagTaxonomy.ResolveCategory(item.Category, item.ArticleTypeLabel) ?? item.Category)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var hasTop = grouped.GetValueOrDefault("top") > 0;
        var hasBottom = grouped.GetValueOrDefault("bottom") > 0;
        var hasShoes = grouped.GetValueOrDefault("shoes") > 0;
        var hasOnePiece = grouped.GetValueOrDefault("dress") > 0 || grouped.GetValueOrDefault("jumpsuit") > 0;

        if (!string.Equals(gender, "women", StringComparison.OrdinalIgnoreCase))
        {
            return [.. RequiredRoleList(hasTop, hasBottom, hasShoes)];
        }

        var hasSeparates = hasTop && hasBottom;
        if (hasShoes && (hasSeparates || hasOnePiece))
        {
            return [];
        }

        if (!hasShoes)
        {
            return ["shoes"];
        }

        if (hasOnePiece)
        {
            return [];
        }

        return [.. RequiredRoleList(hasTop, hasBottom, hasShoes)];
    }

    private static IEnumerable<string> RequiredRoleList(bool hasTop, bool hasBottom, bool hasShoes)
    {
        if (!hasTop)
        {
            yield return "top";
        }

        if (!hasBottom)
        {
            yield return "bottom";
        }

        if (!hasShoes)
        {
            yield return "shoes";
        }
    }

    private static PaletteProfile BuildPaletteProfile(IReadOnlyCollection<string>? palette)
    {
        var exactValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var color in palette ?? [])
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                continue;
            }

            var trimmed = color.Trim().ToLowerInvariant();
            exactValues.Add(trimmed);

            var normalizedFamily = IsHexColor(trimmed)
                ? MapHexToColorFamily(trimmed)
                : ClothingTagTaxonomy.NormalizeColorFamily(trimmed);
            if (!string.IsNullOrWhiteSpace(normalizedFamily))
            {
                families.Add(normalizedFamily);
            }
        }

        return new PaletteProfile(exactValues, families);
    }

    private static string? MapHexToColorFamily(string value)
    {
        if (!value.StartsWith('#') || (value.Length != 7 && value.Length != 4))
        {
            return null;
        }

        var hex = value.Length == 4
            ? new string([value[1], value[1], value[2], value[2], value[3], value[3]])
            : value[1..];

        if (!int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var red)
            || !int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
            || !int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
        {
            return null;
        }

        return RgbToColorFamily(red, green, blue);
    }

    private static bool IsHexColor(string value)
    {
        return value.StartsWith('#') && (value.Length == 7 || value.Length == 4);
    }

    private static string RgbToColorFamily(int red, int green, int blue)
    {
        var max = Math.Max(red, Math.Max(green, blue)) / 255d;
        var min = Math.Min(red, Math.Min(green, blue)) / 255d;
        var delta = max - min;
        var lightness = (max + min) / 2d;

        if (max < 0.18)
        {
            return "black";
        }

        if (lightness > 0.9)
        {
            return "white";
        }

        if (delta < 0.08)
        {
            return "gray";
        }

        var saturation = delta / (1d - Math.Abs((2d * lightness) - 1d));
        if (saturation < 0.16)
        {
            return lightness < 0.42 ? "brown" : "gray";
        }

        double hue;
        if (max == red / 255d)
        {
            hue = 60d * (((green - blue) / (double)(red - Math.Min(green, blue))) % 6d);
        }
        else if (max == green / 255d)
        {
            hue = 60d * (((blue - red) / (double)(green - Math.Min(red, blue))) + 2d);
        }
        else
        {
            hue = 60d * (((red - green) / (double)(blue - Math.Min(red, green))) + 4d);
        }

        if (double.IsNaN(hue))
        {
            return "gray";
        }

        if (hue < 0)
        {
            hue += 360d;
        }

        if (lightness < 0.5 && hue is >= 15 and < 45)
        {
            return "brown";
        }

        return hue switch
        {
            < 15 => "red",
            < 45 => "orange",
            < 70 => "yellow",
            < 170 => "green",
            < 255 => "blue",
            < 315 => "purple",
            _ => "pink"
        };
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

    private static List<OutfitCandidate> BuildCandidates(
        IReadOnlyCollection<WardrobeItemDto> items,
        string style,
        string occasion,
        ResolvedWeatherContext weather,
        string gender,
        PaletteProfile paletteProfile,
        IReadOnlyCollection<string> constraints)
    {
        var wantsLayer = weather.TemperatureC <= 16
            || weather.Status.Contains("rain", StringComparison.OrdinalIgnoreCase)
            || weather.Status.Contains("snow", StringComparison.OrdinalIgnoreCase)
            || constraints.Any(constraint => constraint.Contains("warmer", StringComparison.OrdinalIgnoreCase) || constraint.Contains("layer", StringComparison.OrdinalIgnoreCase));

        var candidates = new List<OutfitCandidate>();
        var tops = RankItems(items.Where(item => ResolveCategory(item) == "top"), style, paletteProfile, prominence: 1.2, preferredCategories: ["top"]);
        var bottoms = RankItems(items.Where(item => ResolveCategory(item) == "bottom"), style, paletteProfile, prominence: 0.7, preferredCategories: ["bottom"]);
        var shoes = RankItems(items.Where(item => ResolveCategory(item) == "shoes"), style, paletteProfile, prominence: 0.65, preferredCategories: ["shoes"]);
        var layers = RankItems(items.Where(item => ResolveCategory(item) == "outerwear"), style, paletteProfile, prominence: 0.75, preferredCategories: ["outerwear"]);
        var accessories = RankItems(items.Where(item => ResolveCategory(item) == "accessories"), style, paletteProfile, prominence: 0.55, preferredCategories: ["accessories"]);

        if (string.Equals(gender, "women", StringComparison.OrdinalIgnoreCase))
        {
            var onePieces = RankItems(
                items.Where(item => ResolveCategory(item) is "dress" or "jumpsuit"),
                style,
                paletteProfile,
                prominence: 1.4,
                preferredCategories: ["dress", "jumpsuit"]);

            foreach (var onePiece in onePieces.Take(5))
            {
                foreach (var shoe in shoes.Take(4))
                {
                    BuildAndAddCandidate(
                        candidates,
                        [onePiece.Item, shoe.Item],
                        [onePiece.Score, shoe.Score],
                        layers,
                        accessories,
                        wantsLayer,
                        style,
                        occasion,
                        weather,
                        paletteProfile,
                        constraints);
                }
            }
        }

        foreach (var top in tops.Take(6))
        {
            foreach (var bottom in bottoms.Take(5))
            {
                foreach (var shoe in shoes.Take(4))
                {
                    BuildAndAddCandidate(
                        candidates,
                        [top.Item, bottom.Item, shoe.Item],
                        [top.Score, bottom.Score, shoe.Score],
                        layers,
                        accessories,
                        wantsLayer,
                        style,
                        occasion,
                        weather,
                        paletteProfile,
                        constraints);
                }
            }
        }

        return SelectDiverseTopCandidates(candidates, 8);
    }

    private static void BuildAndAddCandidate(
        List<OutfitCandidate> candidates,
        IReadOnlyList<WardrobeItemDto> baseItems,
        IReadOnlyList<double> baseScores,
        IReadOnlyList<RankedWardrobeItem> layers,
        IReadOnlyList<RankedWardrobeItem> accessories,
        bool wantsLayer,
        string style,
        string occasion,
        ResolvedWeatherContext weather,
        PaletteProfile paletteProfile,
        IReadOnlyCollection<string> constraints)
    {
        var layerOptions = wantsLayer && layers.Count > 0
            ? layers.Take(3).Select(layer => new CandidateAddition(layer.Item, layer.Score)).Prepend(new CandidateAddition(null, 0d))
            : [new CandidateAddition(null, 0d)];

        var accessoryOptions = accessories.Take(3).Select(accessory => new CandidateAddition(accessory.Item, accessory.Score)).Prepend(new CandidateAddition(null, 0d));

        foreach (var layerOption in layerOptions)
        {
            foreach (var accessoryOption in accessoryOptions)
            {
                var candidateItems = new List<WardrobeItemDto>(baseItems);
                var score = baseScores.Sum();

                if (layerOption.Item is not null)
                {
                    candidateItems.Add(layerOption.Item);
                    score += layerOption.Score;
                }

                if (accessoryOption.Item is not null)
                {
                    candidateItems.Add(accessoryOption.Item);
                    score += accessoryOption.Score;
                }

                score += ScoreCombination(candidateItems, style, weather, paletteProfile, constraints);
                candidates.Add(new OutfitCandidate(candidateItems, score, BuildSummary(style, occasion, weather.Summary, candidateItems)));
            }
        }
    }

    private static List<RankedWardrobeItem> RankItems(
        IEnumerable<WardrobeItemDto> items,
        string style,
        PaletteProfile paletteProfile,
        double prominence,
        IReadOnlyCollection<string> preferredCategories)
    {
        return items
            .Select(item => new RankedWardrobeItem(item, ScoreItem(item, style, paletteProfile, prominence, preferredCategories)))
            .OrderByDescending(item => item.Score)
            .ToList();
    }

    private static double ScoreItem(
        WardrobeItemDto item,
        string targetStyle,
        PaletteProfile paletteProfile,
        double prominence,
        IReadOnlyCollection<string> preferredCategories)
    {
        var score = 0d;
        score += ScoreStyle(item.Style, targetStyle);

        var normalizedColor = ClothingTagTaxonomy.NormalizeColorFamily(item.Color);
        var exactColor = item.Color?.Trim().ToLowerInvariant();
        var isPaletteMatch = (!string.IsNullOrWhiteSpace(exactColor) && paletteProfile.ExactValues.Contains(exactColor))
            || (!string.IsNullOrWhiteSpace(normalizedColor) && paletteProfile.Families.Contains(normalizedColor));
        var isNeutral = !string.IsNullOrWhiteSpace(normalizedColor) && NeutralColors.Contains(normalizedColor);

        if (isPaletteMatch)
        {
            score += 5.5 * prominence;
        }
        else if (isNeutral)
        {
            score += preferredCategories.Contains("bottom") || preferredCategories.Contains("shoes") || preferredCategories.Contains("outerwear")
                ? 2.7
                : 1.2;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedColor))
        {
            score += prominence >= 1 ? 0.8 : -0.4;
        }

        if (preferredCategories.Contains("bottom") && normalizedColor is "blue" or "black" or "white" or "brown")
        {
            score += 1.8;
        }

        if (preferredCategories.Contains("shoes") && normalizedColor is "black" or "white" or "brown")
        {
            score += 1.2;
        }

        if (preferredCategories.Contains("accessories") && normalizedColor is not null && paletteProfile.Families.Contains(normalizedColor))
        {
            score += 1.7;
        }

        if (string.Equals(item.ValidationStatus, "pass", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static double ScoreStyle(string? itemStyle, string targetStyle)
    {
        if (string.IsNullOrWhiteSpace(itemStyle))
        {
            return 1;
        }

        if (itemStyle.Equals(targetStyle, StringComparison.OrdinalIgnoreCase))
        {
            return 4.5;
        }

        return targetStyle.ToLowerInvariant() switch
        {
            "office" when itemStyle.Equals("formal", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 2.3,
            "formal" when itemStyle.Equals("office", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("elegant", StringComparison.OrdinalIgnoreCase) => 2.1,
            "elegant" when itemStyle.Equals("formal", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 2.2,
            "casual" when itemStyle.Equals("streetwear", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("bohemian", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("sport", StringComparison.OrdinalIgnoreCase) => 2,
            "sport" when itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 1.9,
            _ => 0
        };
    }

    private static double ScoreCombination(
        IReadOnlyCollection<WardrobeItemDto> items,
        string style,
        ResolvedWeatherContext weather,
        PaletteProfile paletteProfile,
        IReadOnlyCollection<string> constraints)
    {
        var score = 0d;
        var prominentPiece = items.FirstOrDefault(item => ResolveCategory(item) is "dress" or "jumpsuit")
            ?? items.FirstOrDefault(item => ResolveCategory(item) == "top");
        var bottom = items.FirstOrDefault(item => ResolveCategory(item) == "bottom");
        var shoes = items.FirstOrDefault(item => ResolveCategory(item) == "shoes");
        var layer = items.FirstOrDefault(item => ResolveCategory(item) == "outerwear");
        var accessory = items.FirstOrDefault(item => ResolveCategory(item) == "accessories");

        var prominentColor = ClothingTagTaxonomy.NormalizeColorFamily(prominentPiece?.Color);
        var bottomColor = ClothingTagTaxonomy.NormalizeColorFamily(bottom?.Color);
        var shoeColor = ClothingTagTaxonomy.NormalizeColorFamily(shoes?.Color);
        var layerColor = ClothingTagTaxonomy.NormalizeColorFamily(layer?.Color);
        var accessoryColor = ClothingTagTaxonomy.NormalizeColorFamily(accessory?.Color);

        if (!string.IsNullOrWhiteSpace(prominentColor) && paletteProfile.Families.Contains(prominentColor))
        {
            score += 8;
        }

        if (bottomColor is not null && NeutralColors.Contains(bottomColor))
        {
            score += 3.1;
        }
        else if (!string.IsNullOrWhiteSpace(bottomColor) && paletteProfile.Families.Contains(bottomColor))
        {
            score += 1.8;
        }
        else if (!string.IsNullOrWhiteSpace(bottomColor))
        {
            score -= 1.6;
        }

        if (shoeColor is not null && (NeutralColors.Contains(shoeColor) || shoeColor == prominentColor))
        {
            score += 2.5;
        }

        if (layer is not null)
        {
            score += weather.TemperatureC <= 16 ? 2.8 : -0.7;
            if (layerColor is not null && (NeutralColors.Contains(layerColor) || layerColor == prominentColor))
            {
                score += 1.7;
            }
        }
        else if (weather.TemperatureC <= 12)
        {
            score -= 5;
        }

        if (prominentColor is not null && (prominentColor == shoeColor || prominentColor == layerColor))
        {
            score += 2.4;
        }

        if (accessory is not null)
        {
            if (!string.IsNullOrWhiteSpace(accessoryColor) && (paletteProfile.Families.Contains(accessoryColor) || NeutralColors.Contains(accessoryColor)))
            {
                score += 1.2;
            }
            else
            {
                score -= 0.6;
            }
        }

        var nonNeutralColors = items
            .Select(item => ClothingTagTaxonomy.NormalizeColorFamily(item.Color))
            .Where(color => !string.IsNullOrWhiteSpace(color) && !NeutralColors.Contains(color!))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (nonNeutralColors.Count == 0)
        {
            score += 1.5;
        }
        else if (nonNeutralColors.Count == 1)
        {
            score += 2.4;
        }
        else if (nonNeutralColors.Count == 2)
        {
            score += 0.8;
        }
        else
        {
            score -= 2.7;
        }

        if (constraints.Any(constraint => constraint.Contains("warmer", StringComparison.OrdinalIgnoreCase)) && layer is not null)
        {
            score += 1.1;
        }

        if (constraints.Any(constraint => constraint.Contains("more formal", StringComparison.OrdinalIgnoreCase))
            && items.Any(item => string.Equals(item.Style, "formal", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Style, "office", StringComparison.OrdinalIgnoreCase)))
        {
            score += 1.4;
        }

        if (constraints.Any(constraint => constraint.Contains("more casual", StringComparison.OrdinalIgnoreCase))
            && items.Any(item => string.Equals(item.Style, "casual", StringComparison.OrdinalIgnoreCase)))
        {
            score += 1.4;
        }

        if (style == "sport" && accessory is not null)
        {
            score -= 0.5;
        }

        return score;
    }

    private static List<OutfitCandidate> SelectDiverseTopCandidates(List<OutfitCandidate> candidates, int limit)
    {
        var uniqueCandidates = candidates
            .GroupBy(candidate => string.Join('|', candidate.Items.Select(item => item.Id).OrderBy(id => id)))
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        var selected = new List<OutfitCandidate>();
        while (selected.Count < limit && uniqueCandidates.Count > 0)
        {
            OutfitCandidate? next = null;
            double bestEffectiveScore = double.MinValue;

            foreach (var candidate in uniqueCandidates)
            {
                var overlapPenalty = selected.Count == 0
                    ? 0
                    : selected.Max(existing => CalculateSimilarityPenalty(existing, candidate));

                var effectiveScore = candidate.Score - overlapPenalty;
                if (effectiveScore > bestEffectiveScore)
                {
                    bestEffectiveScore = effectiveScore;
                    next = candidate;
                }
            }

            if (next is null)
            {
                break;
            }

            selected.Add(next);
            uniqueCandidates.Remove(next);
        }

        return selected;
    }

    private static double CalculateSimilarityPenalty(OutfitCandidate existing, OutfitCandidate candidate)
    {
        var sharedItemIds = existing.Items.Select(item => item.Id)
            .Intersect(candidate.Items.Select(item => item.Id), StringComparer.OrdinalIgnoreCase)
            .Count();

        var existingProminentColor = ClothingTagTaxonomy.NormalizeColorFamily(existing.Items.FirstOrDefault(item => ResolveCategory(item) is "dress" or "jumpsuit")?.Color)
            ?? ClothingTagTaxonomy.NormalizeColorFamily(existing.Items.FirstOrDefault(item => ResolveCategory(item) == "top")?.Color);
        var candidateProminentColor = ClothingTagTaxonomy.NormalizeColorFamily(candidate.Items.FirstOrDefault(item => ResolveCategory(item) is "dress" or "jumpsuit")?.Color)
            ?? ClothingTagTaxonomy.NormalizeColorFamily(candidate.Items.FirstOrDefault(item => ResolveCategory(item) == "top")?.Color);

        var sameAnchorColorPenalty = string.Equals(existingProminentColor, candidateProminentColor, StringComparison.OrdinalIgnoreCase)
            ? 2.25
            : 0;

        return (sharedItemIds * 3.2) + sameAnchorColorPenalty;
    }

    private static string BuildSummary(string style, string occasion, string weatherSummary, IReadOnlyCollection<WardrobeItemDto> items)
    {
        var pieceLabels = items
            .Select(item => item.ArticleTypeLabel ?? item.Category)
            .Select(label => label?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Cast<string>()
            .ToList();

        var lead = pieceLabels.Count > 0 ? string.Join(", ", pieceLabels.Take(3)) : "a balanced mix";
        return $"Built as a {style} outfit for {occasion} in {weatherSummary} weather, anchored by {lead}.";
    }

    private static OutfitBoardItemDto MapBoardItem(WardrobeItemDto item)
    {
        return new OutfitBoardItemDto
        {
            Id = item.Id,
            Image = item.Image,
            Category = item.Category,
            ArticleTypeLabel = item.ArticleTypeLabel,
            Color = item.Color,
            Label = item.ArticleTypeLabel ?? item.Category
        };
    }

    private static string? ResolveCategory(WardrobeItemDto item)
    {
        return ClothingTagTaxonomy.ResolveCategory(item.Category, item.ArticleTypeLabel) ?? item.Category;
    }

    private sealed record PaletteProfile(HashSet<string> ExactValues, HashSet<string> Families);
    private sealed record RankedWardrobeItem(WardrobeItemDto Item, double Score);
    private sealed record CandidateAddition(WardrobeItemDto? Item, double Score);
    private sealed record OutfitCandidate(IReadOnlyList<WardrobeItemDto> Items, double Score, string Summary);
}
