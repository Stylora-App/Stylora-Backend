using System.Text.RegularExpressions;
using Stylora.Application.ClothingTags;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.Application.Services;

public class OutfitChatService : IOutfitChatService
{
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

    private static readonly string[] AllowedStyles = ["casual", "office", "sport", "elegant", "bohemian", "streetwear", "formal"];
    private static readonly string[] ScopeKeywords = ["outfit", "wear", "wearing", "look", "dress", "style", "wardrobe", "shuffle", "another option"];
    private static readonly string[] ShuffleKeywords = ["shuffle", "another option", "another look", "different option", "different outfit"];
    private static readonly string[] WeatherStatusKeywords = ["sunny", "cloudy", "rainy", "windy", "snowy", "stormy"];
    private static readonly HashSet<string> NeutralColors = ["black", "white", "gray", "blue", "brown"];

    private readonly IWardrobeService _wardrobeService;
    private readonly IUserService _userService;

    public OutfitChatService(IWardrobeService wardrobeService, IUserService userService)
    {
        _wardrobeService = wardrobeService;
        _userService = userService;
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

        var fullConversation = string.Join(' ', userMessages);
        if (!ContainsScopeIntent(fullConversation))
        {
            return new OutfitChatResponse
            {
                Status = "out_of_scope",
                AssistantMessage = "I can help only with outfit suggestions based on your wardrobe, palette, and weather.",
                SuggestedReplies =
                [
                    "Build me a rainy work outfit",
                    "Create a warm casual look",
                    "Shuffle another option"
                ]
            };
        }

        var context = ParseConversation(userMessages);
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(context.Style))
        {
            missingFields.Add("occasion");
        }

        if (!context.HasWeatherContext)
        {
            missingFields.Add("weather");
        }

        if (missingFields.Count > 0)
        {
            return BuildFollowUpForContext(missingFields);
        }

        var wardrobeItems = (await _wardrobeService.GetAllItemsAsync(userId)).ToList();
        var userProfile = await _userService.GetProfileAsync(userId);

        if (wardrobeItems.Count == 0)
        {
            return new OutfitChatResponse
            {
                Status = "not_enough_pieces",
                AssistantMessage = "Your wardrobe is empty, so I cannot build an outfit yet.",
                MissingRoles = ["top", "bottom", "shoes"],
                SuggestedReplies = ["Open wardrobe and add a few staples", "Add a dress and shoes", "Add a top, bottom, and shoes"]
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
                MissingRoles = missingRoles,
                SuggestedReplies = BuildSuggestionsForMissingRoles(missingRoles)
            };
        }

        var candidates = BuildCandidates(wardrobeItems, userProfile, context.Style!, context.Occasion ?? context.Style!, context.WeatherSummary!, gender);
        if (candidates.Count == 0)
        {
            return new OutfitChatResponse
            {
                Status = "not_enough_pieces",
                AssistantMessage = "I could not assemble a full outfit from the current wardrobe mix.",
                MissingRoles = DetermineMissingRoles(wardrobeItems, gender),
                SuggestedReplies = ["Add more wardrobe staples", "Try a different occasion", "Describe another weather context"]
            };
        }

        var candidateIndex = Math.Min(context.ShuffleCount, candidates.Count - 1);
        var selectedCandidate = candidates[candidateIndex];

        return new OutfitChatResponse
        {
            Status = "success",
            AssistantMessage = candidateIndex == 0
                ? $"I put together a {context.Style} look for {context.Occasion ?? "your plan"} in {context.WeatherSummary} weather."
                : $"Here is another {context.Style} option for {context.Occasion ?? "your plan"} in {context.WeatherSummary} weather.",
            SuggestedReplies = candidates.Count > candidateIndex + 1
                ? ["Shuffle another option", "Make it more casual", "Make it warmer"]
                : ["Describe another occasion", "Make it more formal", "Build a different weather look"],
            Outfit = new OutfitBoardDto
            {
                Occasion = context.Occasion ?? context.Style!,
                Style = context.Style!,
                WeatherSummary = context.WeatherSummary!,
                Gender = gender,
                Summary = selectedCandidate.Summary,
                Palette = userProfile.Palette ?? [],
                Items = selectedCandidate.Items.Select(MapBoardItem).ToList()
            }
        };
    }

    private static OutfitChatResponse BuildFollowUpForContext(List<string> missingFields)
    {
        if (missingFields.SequenceEqual(["occasion", "weather"]))
        {
            return BuildFollowUpResponse(
                missingFields,
                "Tell me the occasion or vibe and what weather I should dress for.",
                ["Work outfit for a rainy day", "Casual sunny weekend look", "Elegant dinner outfit for cool weather"]);
        }

        if (missingFields.Contains("occasion"))
        {
            return BuildFollowUpResponse(
                missingFields,
                "What should the outfit feel like: casual, office, sporty, elegant, or formal?",
                ["Casual", "Office", "Elegant"]);
        }

        return BuildFollowUpResponse(
            missingFields,
            "What weather should I dress for? You can say rainy, sunny, cold, warm, or include a temperature.",
            ["Rainy and cool", "Sunny and warm", "Cold, around 8C"]);
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

    private static bool ContainsScopeIntent(string text)
    {
        return ScopeKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static ParsedConversation ParseConversation(IEnumerable<string> userMessages)
    {
        var parsed = new ParsedConversation();

        foreach (var message in userMessages)
        {
            var lowered = message.ToLowerInvariant();

            if (ShuffleKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                parsed.ShuffleCount++;
            }

            foreach (var style in AllowedStyles)
            {
                if (lowered.Contains(style, StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Style = style;
                    parsed.Occasion ??= style;
                }
            }

            foreach (var occasion in StyleByOccasion)
            {
                if (!lowered.Contains(occasion.Key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                parsed.Occasion ??= occasion.Key;
                parsed.Style ??= occasion.Value;
            }

            var weather = ExtractWeather(message);
            if (weather is not null)
            {
                parsed.HasWeatherContext = true;
                parsed.WeatherSummary = weather;
            }
        }

        return parsed;
    }

    private static string? ExtractWeather(string message)
    {
        var lowered = message.ToLowerInvariant();
        var status = WeatherStatusKeywords.FirstOrDefault(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        string? thermal = null;
        if (lowered.Contains("freezing", StringComparison.OrdinalIgnoreCase))
        {
            thermal = "freezing";
        }
        else if (lowered.Contains("cold", StringComparison.OrdinalIgnoreCase) || lowered.Contains("chilly", StringComparison.OrdinalIgnoreCase))
        {
            thermal = "cold";
        }
        else if (lowered.Contains("cool", StringComparison.OrdinalIgnoreCase))
        {
            thermal = "cool";
        }
        else if (lowered.Contains("warm", StringComparison.OrdinalIgnoreCase))
        {
            thermal = "warm";
        }
        else if (lowered.Contains("hot", StringComparison.OrdinalIgnoreCase))
        {
            thermal = "hot";
        }

        var temperatureMatch = Regex.Match(lowered, @"(-?\d{1,2})\s*(?:°?\s*c|degrees?)");
        if (temperatureMatch.Success && int.TryParse(temperatureMatch.Groups[1].Value, out var temperatureC))
        {
            thermal = temperatureC switch
            {
                <= 4 => "freezing",
                <= 11 => "cold",
                <= 18 => "cool",
                <= 26 => "warm",
                _ => "hot"
            };
        }

        if (status is null && thermal is null)
        {
            return null;
        }

        return string.Join(" ", new[] { status, thermal }.Where(part => !string.IsNullOrWhiteSpace(part)));
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
            .GroupBy(item => item.Category)
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

    private static List<string> BuildSuggestionsForMissingRoles(List<string> missingRoles)
    {
        var suggestions = new List<string>();

        if (missingRoles.Contains("top"))
        {
            suggestions.Add("Add a few tops");
        }

        if (missingRoles.Contains("bottom"))
        {
            suggestions.Add("Add jeans, trousers, or skirts");
        }

        if (missingRoles.Contains("shoes"))
        {
            suggestions.Add("Add at least one pair of shoes");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Add more wardrobe staples");
        }

        return suggestions;
    }

    private static List<OutfitCandidate> BuildCandidates(
        IReadOnlyCollection<WardrobeItemDto> items,
        UserProfileDto userProfile,
        string style,
        string occasion,
        string weatherSummary,
        string gender)
    {
        var wantsLayer = weatherSummary.Contains("cold", StringComparison.OrdinalIgnoreCase)
            || weatherSummary.Contains("cool", StringComparison.OrdinalIgnoreCase)
            || weatherSummary.Contains("freezing", StringComparison.OrdinalIgnoreCase)
            || weatherSummary.Contains("rainy", StringComparison.OrdinalIgnoreCase);

        var palette = new HashSet<string>((userProfile.Palette ?? []).Select(color => color.Trim().ToLowerInvariant()));
        var candidates = new List<OutfitCandidate>();
        var tops = RankItems(items.Where(item => item.Category.Equals("top", StringComparison.OrdinalIgnoreCase)), style, palette, true);
        var bottoms = RankItems(items.Where(item => item.Category.Equals("bottom", StringComparison.OrdinalIgnoreCase)), style, palette, false);
        var shoes = RankItems(items.Where(item => item.Category.Equals("shoes", StringComparison.OrdinalIgnoreCase)), style, palette, false);
        var layers = RankItems(items.Where(item => item.Category.Equals("outerwear", StringComparison.OrdinalIgnoreCase)), style, palette, false);
        var accessories = RankItems(items.Where(item => item.Category.Equals("accessories", StringComparison.OrdinalIgnoreCase)), style, palette, false);

        if (string.Equals(gender, "women", StringComparison.OrdinalIgnoreCase))
        {
            var onePieces = RankItems(
                items.Where(item => item.Category.Equals("dress", StringComparison.OrdinalIgnoreCase) || item.Category.Equals("jumpsuit", StringComparison.OrdinalIgnoreCase)),
                style,
                palette,
                true);

            foreach (var onePiece in onePieces.Take(3))
            {
                foreach (var shoe in shoes.Take(3))
                {
                    var candidateItems = new List<WardrobeItemDto> { onePiece.Item, shoe.Item };
                    var score = onePiece.Score + shoe.Score;

                    if (wantsLayer && layers.Count > 0)
                    {
                        candidateItems.Add(layers[0].Item);
                        score += layers[0].Score;
                    }

                    if (accessories.Count > 0)
                    {
                        candidateItems.Add(accessories[0].Item);
                        score += Math.Max(0, accessories[0].Score - 1);
                    }

                    score += ScoreCombination(candidateItems);
                    candidates.Add(new OutfitCandidate(candidateItems, score, BuildSummary(style, occasion, weatherSummary, candidateItems)));
                }
            }
        }

        foreach (var top in tops.Take(3))
        {
            foreach (var bottom in bottoms.Take(3))
            {
                foreach (var shoe in shoes.Take(3))
                {
                    var candidateItems = new List<WardrobeItemDto> { top.Item, bottom.Item, shoe.Item };
                    var score = top.Score + bottom.Score + shoe.Score;

                    if (wantsLayer && layers.Count > 0)
                    {
                        candidateItems.Add(layers[0].Item);
                        score += layers[0].Score;
                    }

                    if (accessories.Count > 0)
                    {
                        candidateItems.Add(accessories[0].Item);
                        score += Math.Max(0, accessories[0].Score - 1);
                    }

                    score += ScoreCombination(candidateItems);
                    candidates.Add(new OutfitCandidate(candidateItems, score, BuildSummary(style, occasion, weatherSummary, candidateItems)));
                }
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .GroupBy(candidate => string.Join('|', candidate.Items.Select(item => item.Id)))
            .Select(group => group.First())
            .ToList();
    }

    private static List<RankedWardrobeItem> RankItems(IEnumerable<WardrobeItemDto> items, string style, HashSet<string> palette, bool prominent)
    {
        return items
            .Select(item => new RankedWardrobeItem(item, ScoreItem(item, style, palette, prominent)))
            .OrderByDescending(item => item.Score)
            .ToList();
    }

    private static int ScoreItem(WardrobeItemDto item, string targetStyle, HashSet<string> palette, bool prominent)
    {
        var score = 0;
        score += ScoreStyle(item.Style, targetStyle);

        var normalizedColor = ClothingTagTaxonomy.NormalizeColorFamily(item.Color);
        if (!string.IsNullOrWhiteSpace(item.Color) && palette.Contains(item.Color.Trim().ToLowerInvariant()))
        {
            score += prominent ? 3 : 2;
        }
        else if (normalizedColor is not null && NeutralColors.Contains(normalizedColor))
        {
            score += 2;
        }
        else if (prominent && !string.IsNullOrWhiteSpace(item.Color))
        {
            score += 1;
        }

        if (string.Equals(item.ValidationStatus, "pass", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static int ScoreStyle(string? itemStyle, string targetStyle)
    {
        if (string.IsNullOrWhiteSpace(itemStyle))
        {
            return 1;
        }

        if (itemStyle.Equals(targetStyle, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return targetStyle.ToLowerInvariant() switch
        {
            "office" when itemStyle.Equals("formal", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 2,
            "formal" when itemStyle.Equals("office", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("elegant", StringComparison.OrdinalIgnoreCase) => 2,
            "elegant" when itemStyle.Equals("formal", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 2,
            "casual" when itemStyle.Equals("streetwear", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("bohemian", StringComparison.OrdinalIgnoreCase) || itemStyle.Equals("sport", StringComparison.OrdinalIgnoreCase) => 2,
            "sport" when itemStyle.Equals("casual", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 0
        };
    }

    private static int ScoreCombination(IReadOnlyCollection<WardrobeItemDto> items)
    {
        var colors = items
            .Select(item => ClothingTagTaxonomy.NormalizeColorFamily(item.Color))
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .ToList();

        if (colors.Count == 0)
        {
            return 0;
        }

        var nonNeutralColors = colors.Where(color => color is not null && !NeutralColors.Contains(color)).ToList();
        if (nonNeutralColors.Count <= 1)
        {
            return 2;
        }

        return nonNeutralColors.Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 2 ? 1 : -1;
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

    private sealed class ParsedConversation
    {
        public string? Occasion { get; set; }
        public string? Style { get; set; }
        public bool HasWeatherContext { get; set; }
        public string? WeatherSummary { get; set; }
        public int ShuffleCount { get; set; }
    }

    private sealed record RankedWardrobeItem(WardrobeItemDto Item, int Score);
    private sealed record OutfitCandidate(IReadOnlyList<WardrobeItemDto> Items, int Score, string Summary);
}
