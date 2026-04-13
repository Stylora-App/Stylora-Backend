namespace Stylora.Application.Models;

/// <summary>
/// Gender-specific category → ASOS search-term mappings.
/// </summary>
public static class CategoryData
{
    public static readonly Dictionary<string, string> WomenCategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "top blouse shirt" },
            { "bottoms",     "pants trousers skirt jeans" },
            { "dresses",     "dress" },
            { "shoes",       "shoes boots heels sneakers" },
            { "accessories", "accessories bag handbag jewelry" },
            { "outerwear",   "coat jacket blazer" },
        };

    public static readonly Dictionary<string, string> MenCategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "shirt t-shirt polo sweatshirt jumper" },
            { "bottoms",     "pants trousers jeans chinos shorts" },
            { "shoes",       "shoes boots sneakers trainers loafers" },
            { "accessories", "accessories watch belt bag backpack" },
            { "outerwear",   "coat jacket blazer puffer" },
        };
}
