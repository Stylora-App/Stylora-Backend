using System.Globalization;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class ClothingValidationService : IClothingValidationService
{
    private static readonly Dictionary<string, string> UsageToStyleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["casual"] = "casual",
        ["smart casual"] = "casual",
        ["travel"] = "casual",
        ["sports"] = "sport",
        ["formal"] = "formal",
        ["party"] = "elegant",
        ["ethnic"] = "elegant",
    };

    private static readonly HashSet<string> UnisexFriendlyArticleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "t-shirt",
        "tee",
        "long sleeve top",
        "shirt",
        "overshirt",
        "hoodie",
        "sweatshirt",
        "jumper",
        "sweater",
        "cardigan",
        "coat",
        "jacket",
        "blazer",
        "jeans",
        "trousers",
        "pants",
        "shorts",
        "skirt",
        "sneakers",
        "boots",
        "sandals",
        "shoes",
    };

    private static readonly HashSet<string> WomenBiasedArticleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dress",
        "jumpsuit",
        "romper",
        "cami dress",
        "slip dress",
        "skirt",
        "blouse",
        "heels",
    };

    private static readonly HashSet<string> MenBiasedArticleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "polo",
        "dress shirt",
        "waistcoat",
        "suit trousers",
        "boxers",
    };

    private readonly IImageEmbeddingService _imageEmbeddingService;
    private readonly IClothingReferenceEmbeddingRepository _referenceRepository;
    private readonly ClothingValidationSettings _settings;
    private readonly ILogger<ClothingValidationService> _logger;

    public ClothingValidationService(
        IImageEmbeddingService imageEmbeddingService,
        IClothingReferenceEmbeddingRepository referenceRepository,
        ClothingValidationSettings settings,
        ILogger<ClothingValidationService> logger)
    {
        _imageEmbeddingService = imageEmbeddingService;
        _referenceRepository = referenceRepository;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ClothingImageValidationResult> ValidateAsync(string imageBase64, CancellationToken cancellationToken = default)
    {
        float[] embedding;
        try
        {
            embedding = await _imageEmbeddingService.EmbedImageAsync(imageBase64, cancellationToken);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsWorkerStartupFailure(ex))
        {
            _logger.LogWarning(ex, "Clothing validation worker is not ready yet.");
            return CreateWorkerUnavailableResult();
        }

        var neighbors = await _referenceRepository.GetNearestNeighborsAsync(embedding, _settings.TopK, cancellationToken);
        if (neighbors.Count == 0)
        {
            neighbors = await _referenceRepository.GetNearestNeighborsByScanAsync(embedding, _settings.TopK, cancellationToken);
        }

        if (neighbors.Count == 0)
        {
            return new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Warning,
                IsLikelyClothing = false,
                Confidence = 0,
                Message = "Clothing validation is not ready yet for this image, but you can still save it.",
                NearestLabels = []
            };
        }

        var scoredNeighbors = neighbors
            .Select(match => new ScoredMatch(match, ScoreWeight(match.Distance)))
            .ToList();
        var clothingNeighbors = scoredNeighbors
            .Where(entry => entry.Match.Label == ClothingReferenceLabel.Clothing)
            .ToList();

        var clothingScore = scoredNeighbors
            .Where(entry => entry.Match.Label == ClothingReferenceLabel.Clothing)
            .Sum(entry => entry.Weight);
        var nonClothingScore = scoredNeighbors
            .Where(entry => entry.Match.Label == ClothingReferenceLabel.NonClothing)
            .Sum(entry => entry.Weight);
        var totalScore = clothingScore + nonClothingScore;
        var clothingShare = totalScore <= 0 ? 0.5 : clothingScore / totalScore;
        var margin = Math.Abs(clothingShare - 0.5d) * 2d;
        var isLikelyClothing = clothingScore >= nonClothingScore;
        var confidence = Math.Clamp(isLikelyClothing ? clothingShare : 1d - clothingShare, 0d, 1d);

        var status = isLikelyClothing &&
                     clothingShare >= _settings.MinimumClothingShare &&
                     margin >= _settings.MinimumMargin
            ? ClothingValidationStatus.Pass
            : ClothingValidationStatus.Warning;

        var message = status == ClothingValidationStatus.Pass
            ? "This upload looks like a clothing item."
            : isLikelyClothing
                ? "This image might be a clothing item, but the validator is not confident. You can still save it."
                : "This image does not look like a clothing item to the validator. You can still save it if that is intentional.";

        var suggestedArticleType = ResolveArticleType(clothingNeighbors);
        var suggestedCategory = ResolveCategory(suggestedArticleType, clothingNeighbors);
        var extractedColor = TryExtractColor(imageBase64, suggestedCategory);
        var suggestedUsage = Vote(clothingNeighbors, match => NormalizeUsage(match.UsageTag));
        var suggestedStyle = suggestedUsage is null
            ? null
            : UsageToStyleMap.GetValueOrDefault(suggestedUsage);
        var suggestedGender = ResolveGender(clothingNeighbors, suggestedArticleType);

        return new ClothingImageValidationResult
        {
            Status = status,
            IsLikelyClothing = isLikelyClothing,
            Confidence = Math.Round(confidence, 4),
            Message = message,
            NearestLabels = neighbors.Select(match => match.Label == ClothingReferenceLabel.Clothing ? "clothing" : "non_clothing").ToList(),
            SuggestedCategory = suggestedCategory,
            SuggestedArticleType = suggestedArticleType,
            SuggestedStyle = suggestedStyle,
            SuggestedColor = extractedColor?.DisplayName ?? Vote(clothingNeighbors, match => NormalizeDisplayColor(match.BaseColour)),
            SuggestedColorFamily = extractedColor?.Family ?? Vote(clothingNeighbors, match => NormalizeValue(match.ColorFamily)),
            SuggestedUsage = suggestedUsage,
            SuggestedGender = suggestedGender
        };
    }

    private static bool IsWorkerStartupFailure(Exception exception)
    {
        return exception is TimeoutException ||
               exception is HttpRequestException ||
               exception is InvalidOperationException;
    }

    private static ClothingImageValidationResult CreateWorkerUnavailableResult()
    {
        return new ClothingImageValidationResult
        {
            Status = ClothingValidationStatus.Warning,
            IsLikelyClothing = false,
            Confidence = 0,
            Message = "Clothing validation is still warming up. Please retry in a moment, or continue and save the item anyway.",
            NearestLabels = []
        };
    }

    private static double ScoreWeight(double distance)
    {
        var similarity = Math.Max(0d, 1d - distance);
        return similarity * similarity;
    }

    private static string? ResolveArticleType(IReadOnlyList<ScoredMatch> clothingNeighbors)
    {
        if (clothingNeighbors.Count == 0)
        {
            return null;
        }

        var closest = clothingNeighbors
            .OrderByDescending(entry => entry.Weight)
            .Take(2)
            .Select(entry => NormalizeArticleType(entry.Match.ArticleType ?? entry.Match.CategoryHint))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (closest.Count == 2 && string.Equals(closest[0], closest[1], StringComparison.OrdinalIgnoreCase))
        {
            return closest[0];
        }

        return Vote(clothingNeighbors, match => NormalizeArticleType(match.ArticleType ?? match.CategoryHint));
    }

    private static string? ResolveCategory(string? articleType, IReadOnlyList<ScoredMatch> clothingNeighbors)
    {
        var categoryFromArticleType = MapBroadCategory(articleType);
        if (!string.IsNullOrWhiteSpace(categoryFromArticleType))
        {
            return categoryFromArticleType;
        }

        return Vote(clothingNeighbors, match => NormalizeCategory(match.CategoryGroup) ?? MapBroadCategory(NormalizeArticleType(match.ArticleType ?? match.CategoryHint)));
    }

    private static string? ResolveGender(IReadOnlyList<ScoredMatch> clothingNeighbors, string? articleType)
    {
        if (clothingNeighbors.Count == 0)
        {
            return null;
        }

        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var neighbor in clothingNeighbors)
        {
            var normalizedGender = NormalizeGender(neighbor.Match.GenderTag);
            if (normalizedGender is null)
            {
                continue;
            }

            scores[normalizedGender] = scores.GetValueOrDefault(normalizedGender) + neighbor.Weight;
        }

        if (scores.Count == 0)
        {
            return null;
        }

        var best = scores.OrderByDescending(entry => entry.Value).First();
        var second = scores
            .Where(entry => !entry.Key.Equals(best.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Value)
            .FirstOrDefault();

        var normalizedArticleType = NormalizeArticleType(articleType);
        if (normalizedArticleType is not null)
        {
            if (WomenBiasedArticleTypes.Contains(normalizedArticleType))
            {
                return "women";
            }

            if (MenBiasedArticleTypes.Contains(normalizedArticleType) &&
                best.Value >= (second.Value > 0 ? second.Value * 1.15d : 0.2d))
            {
                return "men";
            }

            if (UnisexFriendlyArticleTypes.Contains(normalizedArticleType))
            {
                if (scores.TryGetValue("unisex", out var unisexScore) && unisexScore >= best.Value * 0.6d)
                {
                    return "unisex";
                }

                if (best.Key is "men" or "women")
                {
                    if (second.Value >= best.Value * 0.45d || second.Value == 0d)
                    {
                        return "unisex";
                    }
                }
            }
        }

        if (best.Key == "unisex")
        {
            return "unisex";
        }

        if (second.Value >= best.Value * 0.7d)
        {
            return "unisex";
        }

        return best.Key;
    }

    private static ExtractedColor? TryExtractColor(string imageBase64, string? broadCategory)
    {
        try
        {
            var bytes = DecodeImageBytes(imageBase64);
            using var image = Image.Load<Rgba32>(bytes);
            var bounds = ResolveColorSamplingBounds(image.Width, image.Height, broadCategory);
            var histogram = new Dictionary<string, ColorBucket>(StringComparer.OrdinalIgnoreCase);

            var stepX = Math.Max(1, bounds.Width / 72);
            var stepY = Math.Max(1, bounds.Height / 72);

            for (var y = bounds.Top; y < bounds.Bottom; y += stepY)
            {
                for (var x = bounds.Left; x < bounds.Right; x += stepX)
                {
                    var pixel = image[x, y];
                    if (pixel.A < 32)
                    {
                        continue;
                    }

                    var hsv = ToHsv(pixel);
                    if (ShouldIgnorePixel(hsv))
                    {
                        continue;
                    }

                    var family = DetermineColorFamily(hsv);
                    if (family is null)
                    {
                        continue;
                    }

                    if (!histogram.TryGetValue(family, out var bucket))
                    {
                        bucket = new ColorBucket();
                        histogram[family] = bucket;
                    }

                    var weight = 1d;
                    if (Math.Abs(x - image.Width / 2d) < image.Width * 0.18d)
                    {
                        weight += 0.3d;
                    }

                    bucket.Weight += weight;
                    bucket.HueSum += hsv.Hue * weight;
                    bucket.SaturationSum += hsv.Saturation * weight;
                    bucket.ValueSum += hsv.Value * weight;
                }
            }

            if (histogram.Count == 0)
            {
                return null;
            }

            var winner = histogram.OrderByDescending(entry => entry.Value.Weight).First();
            return new ExtractedColor(
                DetermineDisplayColorName(winner.Key, winner.Value),
                winner.Key);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DecodeImageBytes(string imageBase64)
    {
        var trimmed = imageBase64.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..];
        }

        return Convert.FromBase64String(trimmed);
    }

    private static SamplingBounds ResolveColorSamplingBounds(int width, int height, string? broadCategory)
    {
        var normalized = NormalizeCategory(broadCategory) ?? "top";
        return normalized switch
        {
            "bottom" => new SamplingBounds(
                (int)(width * 0.22),
                (int)(height * 0.42),
                (int)(width * 0.78),
                (int)(height * 0.96)),
            "dress" or "jumpsuit" => new SamplingBounds(
                (int)(width * 0.22),
                (int)(height * 0.14),
                (int)(width * 0.78),
                (int)(height * 0.96)),
            "shoes" => new SamplingBounds(
                (int)(width * 0.2),
                (int)(height * 0.62),
                (int)(width * 0.8),
                (int)(height * 0.98)),
            "accessories" => new SamplingBounds(
                (int)(width * 0.16),
                (int)(height * 0.16),
                (int)(width * 0.84),
                (int)(height * 0.84)),
            _ => new SamplingBounds(
                (int)(width * 0.2),
                (int)(height * 0.14),
                (int)(width * 0.8),
                (int)(height * 0.7)),
        };
    }

    private static bool ShouldIgnorePixel(HsvColor hsv)
    {
        if (hsv.Value >= 0.94f && hsv.Saturation <= 0.12f)
        {
            return true;
        }

        if (hsv.Value >= 0.86f && hsv.Saturation <= 0.05f)
        {
            return true;
        }

        return false;
    }

    private static HsvColor ToHsv(Rgba32 pixel)
    {
        var r = pixel.R / 255f;
        var g = pixel.G / 255f;
        var b = pixel.B / 255f;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        float hue;
        if (delta == 0f)
        {
            hue = 0f;
        }
        else if (Math.Abs(max - r) < 0.0001f)
        {
            hue = 60f * (((g - b) / delta) % 6f);
        }
        else if (Math.Abs(max - g) < 0.0001f)
        {
            hue = 60f * (((b - r) / delta) + 2f);
        }
        else
        {
            hue = 60f * (((r - g) / delta) + 4f);
        }

        if (hue < 0f)
        {
            hue += 360f;
        }

        var saturation = max == 0f ? 0f : delta / max;
        return new HsvColor(hue, saturation, max);
    }

    private static string? DetermineColorFamily(HsvColor hsv)
    {
        if (hsv.Value <= 0.18f)
        {
            return "black";
        }

        if (hsv.Saturation <= 0.12f)
        {
            if (hsv.Value >= 0.88f)
            {
                return "white";
            }

            return hsv.Value >= 0.5f ? "gray" : "black";
        }

        if (hsv.Value <= 0.48f && hsv.Hue is >= 12f and <= 48f)
        {
            return "brown";
        }

        return hsv.Hue switch
        {
            < 12f => "red",
            < 25f => "orange",
            < 48f => "yellow",
            < 78f => "green",
            < 170f => "green",
            < 255f => "blue",
            < 320f => "purple",
            _ => "red",
        };
    }

    private static string DetermineDisplayColorName(string family, ColorBucket bucket)
    {
        var averageHue = bucket.Weight <= 0 ? 0 : bucket.HueSum / bucket.Weight;
        var averageValue = bucket.Weight <= 0 ? 0 : bucket.ValueSum / bucket.Weight;

        return family switch
        {
            "blue" when averageValue < 0.42d => "navy blue",
            "blue" when averageValue > 0.72d => "light blue",
            "red" when averageValue < 0.4d => "burgundy",
            "brown" when averageValue > 0.62d => "beige",
            "gray" when averageValue > 0.78d => "light gray",
            "gray" when averageValue < 0.32d => "charcoal",
            "green" when averageHue >= 70d && averageHue <= 95d => "olive",
            "purple" when averageHue >= 300d && averageValue > 0.75d => "pink",
            _ => family switch
            {
                "black" => "black",
                "white" => "white",
                "gray" => "gray",
                "red" => "red",
                "orange" => "orange",
                "yellow" => "yellow",
                "green" => "green",
                "blue" => "blue",
                "purple" => "purple",
                "brown" => "brown",
                _ => family,
            }
        };
    }

    private static string? Vote(IEnumerable<ScoredMatch> neighbors, Func<ClothingReferenceMatch, string?> selector)
    {
        return neighbors
            .Select(entry => new
            {
                Value = selector(entry.Match),
                entry.Weight
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && item.Weight > 0d)
            .GroupBy(item => item.Value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(item => item.Weight))
            .ThenByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string? NormalizeArticleType(string? articleType)
    {
        var normalized = NormalizeValue(articleType);
        if (normalized is null)
        {
            return null;
        }

        normalized = normalized.Replace("tshirts", "t-shirts", StringComparison.OrdinalIgnoreCase);
        return normalized switch
        {
            "t-shirts" or "t-shirt" or "tees" or "tee" => "t-shirt",
            "shirts" => "shirt",
            "tops" => "top",
            "tunics" => "tunic",
            "blouses" => "blouse",
            "kurtas" => "kurta",
            "sweatshirts" => "sweatshirt",
            "sweaters" => "sweater",
            "jumpers" => "jumper",
            "cardigans" => "cardigan",
            "jackets" => "jacket",
            "coats" => "coat",
            "blazers" => "blazer",
            "dresses" => "dress",
            "jeans" => "jeans",
            "trousers" => "trousers",
            "pants" or "track pants" => "pants",
            "shorts" => "shorts",
            "skirts" => "skirt",
            "jumpsuits" => "jumpsuit",
            "rompers" => "romper",
            "night suits" => "lounge set",
            "casual shoes" or "sports shoes" or "formal shoes" => "shoes",
            "flats" => "flats",
            "heels" => "heels",
            "sandals" => "sandals",
            "boots" => "boots",
            "sneakers" => "sneakers",
            "watches" => "watch",
            "bags" => "bag",
            "belts" => "belt",
            "sunglasses" => "sunglasses",
            "caps" => "cap",
            _ when normalized.Contains("long sleeve", StringComparison.OrdinalIgnoreCase) => "long sleeve top",
            _ when normalized.Contains("dress", StringComparison.OrdinalIgnoreCase) => "dress",
            _ when normalized.Contains("jumpsuit", StringComparison.OrdinalIgnoreCase) => "jumpsuit",
            _ when normalized.Contains("romper", StringComparison.OrdinalIgnoreCase) => "romper",
            _ when normalized.Contains("shirt", StringComparison.OrdinalIgnoreCase) => "shirt",
            _ when normalized.Contains("blouse", StringComparison.OrdinalIgnoreCase) => "blouse",
            _ when normalized.Contains("cardigan", StringComparison.OrdinalIgnoreCase) => "cardigan",
            _ when normalized.Contains("hoodie", StringComparison.OrdinalIgnoreCase) => "hoodie",
            _ when normalized.Contains("sweatshirt", StringComparison.OrdinalIgnoreCase) => "sweatshirt",
            _ when normalized.Contains("jacket", StringComparison.OrdinalIgnoreCase) => "jacket",
            _ when normalized.Contains("coat", StringComparison.OrdinalIgnoreCase) => "coat",
            _ when normalized.Contains("blazer", StringComparison.OrdinalIgnoreCase) => "blazer",
            _ when normalized.Contains("jean", StringComparison.OrdinalIgnoreCase) => "jeans",
            _ when normalized.Contains("trouser", StringComparison.OrdinalIgnoreCase) => "trousers",
            _ when normalized.Contains("pant", StringComparison.OrdinalIgnoreCase) => "pants",
            _ when normalized.Contains("short", StringComparison.OrdinalIgnoreCase) => "shorts",
            _ when normalized.Contains("skirt", StringComparison.OrdinalIgnoreCase) => "skirt",
            _ when normalized.Contains("shoe", StringComparison.OrdinalIgnoreCase) => "shoes",
            _ when normalized.Contains("sneaker", StringComparison.OrdinalIgnoreCase) => "sneakers",
            _ when normalized.Contains("boot", StringComparison.OrdinalIgnoreCase) => "boots",
            _ when normalized.Contains("sandal", StringComparison.OrdinalIgnoreCase) => "sandals",
            _ => normalized
        };
    }

    private static string? MapBroadCategory(string? articleType)
    {
        var normalized = NormalizeArticleType(articleType);
        if (normalized is null)
        {
            return null;
        }

        return normalized switch
        {
            "dress" or "cami dress" or "slip dress" => "dress",
            "jumpsuit" or "romper" => "jumpsuit",
            "jeans" or "trousers" or "pants" or "shorts" or "skirt" => "bottom",
            "jacket" or "coat" or "blazer" or "hoodie" or "sweatshirt" or "cardigan" => "outerwear",
            "shoes" or "sneakers" or "boots" or "sandals" or "heels" or "flats" => "shoes",
            "watch" or "bag" or "belt" or "sunglasses" or "cap" => "accessories",
            _ => normalized switch
            {
                "top" or "t-shirt" or "shirt" or "blouse" or "kurta" or "tunic" or "long sleeve top" or "sweater" or "jumper" or "polo" => "top",
                _ => null
            }
        };
    }

    private static string? NormalizeCategory(string? category)
    {
        var normalized = NormalizeValue(category);
        return normalized switch
        {
            "tops" => "top",
            "bottoms" => "bottom",
            "dresses" => "dress",
            "jumpsuits" => "jumpsuit",
            "shoe" => "shoes",
            "accessory" => "accessories",
            _ => normalized
        };
    }

    private static string? NormalizeUsage(string? usage)
    {
        var normalized = NormalizeValue(usage);
        return normalized switch
        {
            "smart casual" => "casual",
            "party" => "elegant",
            _ => normalized
        };
    }

    private static string? NormalizeGender(string? gender)
    {
        var normalized = NormalizeValue(gender);
        return normalized switch
        {
            "men" => "men",
            "women" => "women",
            "unisex" => "unisex",
            _ => null
        };
    }

    private static string? NormalizeDisplayColor(string? value)
    {
        var normalized = NormalizeValue(value);
        if (normalized is null)
        {
            return null;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private sealed record ScoredMatch(ClothingReferenceMatch Match, double Weight);

    private sealed class ColorBucket
    {
        public double Weight { get; set; }
        public double HueSum { get; set; }
        public double SaturationSum { get; set; }
        public double ValueSum { get; set; }
    }

    private sealed record ExtractedColor(string DisplayName, string Family);
    private sealed record HsvColor(float Hue, float Saturation, float Value);
    private sealed record SamplingBounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Math.Max(1, Right - Left);
        public int Height => Math.Max(1, Bottom - Top);
    }
}
