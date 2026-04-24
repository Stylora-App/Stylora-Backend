namespace Stylora.Application.Models;

/// <summary>
/// Gender-specific category → ASOS search-term mappings.
/// </summary>
public static class CategoryData
{
    public static readonly Dictionary<string, string> WomenCategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "top blouse shirt t-shirt knit cardigan cami bodysuit" },
            { "bottoms",     "pants trousers skirt jeans tailored shorts" },
            { "dresses",     "dress midi maxi mini occasion" },
            { "shoes",       "shoes boots heels flats sandals sneakers loafers" },
            { "accessories", "accessories bag handbag crossbody jewelry sunglasses belt scarf" },
            { "outerwear",   "coat jacket blazer trench puffer shacket" },
        };

    public static readonly Dictionary<string, string> MenCategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "shirt t-shirt polo sweatshirt jumper knit overshirt" },
            { "bottoms",     "pants trousers jeans chinos tailored shorts cargo" },
            { "shoes",       "shoes boots sneakers trainers loafers derby" },
            { "accessories", "accessories watch belt bag backpack cap scarf" },
            { "outerwear",   "coat jacket blazer puffer parka overshirt" },
        };
}
