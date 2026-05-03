using Stylora.Domain.Enums;

namespace Stylora.Application.ClothingTags;

public static class ClothingTagTaxonomy
{
    public static string? NormalizeArticleType(string? articleType)
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

    public static string? NormalizeCategory(string? category)
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

    public static string? ResolveCategory(string? category, string? articleType)
    {
        var normalizedCategory = NormalizeCategory(category);
        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            return normalizedCategory;
        }

        var normalizedArticleType = NormalizeArticleType(articleType);
        if (normalizedArticleType is null)
        {
            return null;
        }

        return normalizedArticleType switch
        {
            "dress" or "cami dress" or "slip dress" => "dress",
            "jumpsuit" or "romper" => "jumpsuit",
            "jeans" or "trousers" or "pants" or "shorts" or "skirt" => "bottom",
            "jacket" or "coat" or "blazer" or "hoodie" or "sweatshirt" or "cardigan" => "outerwear",
            "shoes" or "sneakers" or "boots" or "sandals" or "heels" or "flats" => "shoes",
            "watch" or "bag" or "belt" or "sunglasses" or "cap" => "accessories",
            "top" or "t-shirt" or "shirt" or "blouse" or "kurta" or "tunic" or "long sleeve top" or "sweater" or "jumper" or "polo" => "top",
            _ => null
        };
    }

    public static string? NormalizeAudienceTag(string? audienceTag)
    {
        var normalized = NormalizeValue(audienceTag);
        return normalized switch
        {
            "men" => "men",
            "women" => "women",
            "unisex" => "unisex",
            _ => null
        };
    }

    public static string? NormalizeUsageTag(string? usageTag)
    {
        var normalized = NormalizeValue(usageTag);
        return normalized switch
        {
            "sports" => "sport",
            "smart casual" => "casual",
            "party" => "elegant",
            _ => normalized
        };
    }

    public static string? NormalizeColorFamily(string? value)
    {
        var normalized = NormalizeValue(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized switch
        {
            var color when ContainsAny(color, "black") => "black",
            var color when ContainsAny(color, "white", "off white", "cream") => "white",
            var color when ContainsAny(color, "grey", "gray", "silver", "charcoal") => "gray",
            var color when ContainsAny(color, "brown", "khaki", "tan", "beige", "taupe", "camel") => "brown",
            var color when ContainsAny(color, "blue", "navy", "teal", "turquoise") => "blue",
            var color when ContainsAny(color, "green", "olive", "lime") => "green",
            var color when ContainsAny(color, "red", "maroon", "burgundy") => "red",
            var color when ContainsAny(color, "pink", "magenta", "peach", "coral", "rose") => "pink",
            var color when ContainsAny(color, "purple", "lavender", "violet") => "purple",
            var color when ContainsAny(color, "orange", "rust") => "orange",
            var color when ContainsAny(color, "yellow", "mustard", "gold") => "yellow",
            var color when ContainsAny(color, "multi") => "multicolor",
            _ => normalized
        };
    }

    public static string? ResolveOutfitRole(string? category, string? articleType)
    {
        var normalizedCategory = ResolveCategory(category, articleType);
        return normalizedCategory switch
        {
            "top" => ToTag(OutfitRole.UpperBody),
            "bottom" => ToTag(OutfitRole.LowerBody),
            "dress" or "jumpsuit" => ToTag(OutfitRole.OnePiece),
            "outerwear" => ToTag(OutfitRole.Layer),
            "shoes" => ToTag(OutfitRole.Footwear),
            "accessories" => ToTag(OutfitRole.Accessory),
            _ => null
        };
    }

    public static string? InferArticleTypeFromProductName(string? productName)
    {
        return NormalizeArticleType(productName);
    }

    public static string ToTag(OutfitRole outfitRole)
    {
        return outfitRole switch
        {
            OutfitRole.UpperBody => "upper_body",
            OutfitRole.LowerBody => "lower_body",
            OutfitRole.OnePiece => "one_piece",
            OutfitRole.Layer => "layer",
            OutfitRole.Footwear => "footwear",
            OutfitRole.Accessory => "accessory",
            _ => throw new ArgumentOutOfRangeException(nameof(outfitRole), outfitRole, null)
        };
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
