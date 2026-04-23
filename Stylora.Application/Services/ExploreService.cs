using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Application.Services;

public class ExploreService : IExploreService
{
    private readonly IAsosService _asosService;

    private const int AsosBatchSize = 48;

    public ExploreService(IAsosService asosService)
    {
        _asosService = asosService;
    }

    public async Task<ExploreResultDto> SearchAsync(ExploreQueryDto query)
    {
        var pageSize   = Math.Clamp(query.PageSize, 1, AsosBatchSize);
        var searchTerm = BuildBrowseQuery(query);
        var families = BuildPaletteColorFamilies(query.Palette);
        var familiesWithNeutrals = BuildFamiliesWithNeutrals(families);
        var skipCount = (Math.Max(query.Page, 1) - 1) * pageSize;
        var collectedProducts = new List<ShoppingProductDto>(pageSize + 1);
        var filteredSeen = 0;
        var asosOffset = 0;
        var hasMoreRawResults = true;

        while (hasMoreRawResults && collectedProducts.Count <= pageSize)
        {
            var raw = await _asosService.SearchProductsAsync(searchTerm, AsosBatchSize, asosOffset);
            hasMoreRawResults = raw.Count == AsosBatchSize;

            foreach (var product in ApplyFilters(raw, query.Gender, families, familiesWithNeutrals, query.Q))
            {
                if (filteredSeen < skipCount)
                {
                    filteredSeen++;
                    continue;
                }

                collectedProducts.Add(product);
                if (collectedProducts.Count > pageSize)
                    break;
            }

            if (raw.Count < AsosBatchSize || collectedProducts.Count > pageSize)
                break;

            asosOffset += AsosBatchSize;
        }

        return new ExploreResultDto
        {
            Products = collectedProducts.Take(pageSize).ToList(),
            HasMore  = collectedProducts.Count > pageSize,
            Page     = query.Page,
            PageSize = pageSize,
        };
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    private static string BuildBrowseQuery(ExploreQueryDto query)
    {
        var gender   = GenderTerm(query.Gender);
        var category = BuildCategoryTerm(query.Category, query.Gender);
        return $"{gender} {category}".Trim();
    }

    private static string GenderTerm(string? gender) => gender?.ToLowerInvariant() switch
    {
        "women" => "women",
        "men"   => "men",
        _       => string.Empty,
    };

    private static string BuildCategoryTerm(string? category, string? gender)
    {
        if (string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase))
            return "fashion outfit";

        var isMen = gender?.Equals("men", StringComparison.OrdinalIgnoreCase) == true;
        if (isMen && CategoryData.MenCategoryTerms.TryGetValue(category, out var menTerm))
            return menTerm;

        return CategoryData.WomenCategoryTerms.GetValueOrDefault(category, "fashion");
    }

    // ── Hex → nearest named colour ────────────────────────────────────────────

    // ── Colour family matching ─────────────────────────────────────────────────

    private static HashSet<string> BuildPaletteColorFamilies(List<string>? palette)
    {
        if (palette is null || palette.Count == 0) return [];

        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hex in palette)
        {
            var name = HexToColorName(hex);
            if (name is null) continue;

            foreach (var (family, keywords) in ColourData.ColourFamilyKeywords)
            {
                if (keywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    families.Add(family);
                    break;
                }
            }

            if (ColourData.ColourFamilyKeywords.ContainsKey(name)) families.Add(name);
        }
        return families;
    }

    private static HashSet<string>? BuildFamiliesWithNeutrals(HashSet<string> families)
    {
        if (families.Count == 0) return null;

        var withNeutrals = new HashSet<string>(families, StringComparer.OrdinalIgnoreCase);
        foreach (var neutralFamily in ColourData.NeutralFamilies)
            withNeutrals.Add(neutralFamily);

        return withNeutrals;
    }

    private static IEnumerable<ShoppingProductDto> ApplyFilters(
        IEnumerable<ShoppingProductDto> products,
        string? gender,
        HashSet<string> paletteFamilies,
        HashSet<string>? paletteFamiliesWithNeutrals,
        string? searchQuery)
    {
        foreach (var product in products)
        {
            if (IsExcluded(product, gender))
                continue;

            if (paletteFamiliesWithNeutrals is not null)
            {
                if (!IsColourMatch(product.Colour, paletteFamiliesWithNeutrals))
                    continue;

                product.PaletteMatch = IsColourMatch(product.Colour, paletteFamilies);
            }
            else
            {
                product.PaletteMatch = false;
            }

            if (!MatchesSearchQuery(product, searchQuery))
                continue;

            yield return product;
        }
    }

    private static bool MatchesSearchQuery(ShoppingProductDto product, string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return true;

        var haystack = string.Join(' ', [product.Name, product.BrandName, product.Colour ?? string.Empty])
            .ToLowerInvariant();

        return searchQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.ToLowerInvariant())
            .All(haystack.Contains);
    }

    private static bool IsColourMatch(string? colour, HashSet<string> families)
    {
        if (string.IsNullOrWhiteSpace(colour)) return false;
        var lower = colour.ToLowerInvariant();
        return families.Any(family =>
            ColourData.ColourFamilyKeywords.TryGetValue(family, out var kws) &&
            kws.Any(kw => lower.Contains(kw)));
    }

    private static string? HexToColorName(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return null;

        try
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);

            return ColourData.NamedColours
                .OrderBy(c => RgbDistSq(r, g, b, c.R, c.G, c.B))
                .First()
                .Name;
        }
        catch { return null; }
    }

    private static int RgbDistSq(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = r1 - r2; var dg = g1 - g2; var db = b1 - b2;
        return dr * dr + dg * dg + db * db;
    }

    // ── Exclusion filter ──────────────────────────────────────────────────────

    private static bool IsExcluded(ShoppingProductDto p, string? gender)
    {
        var name = p.Name.ToLowerInvariant();
        if (FilterData.ExcludeTerms.Any(t => name.Contains(t))) return true;

        if (gender?.Equals("men", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (FilterData.ExcludeTermsMen.Any(t => name.Contains(t))) return true;
        }
        else if (gender?.Equals("women", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (FilterData.ExcludeTermsWomen.Any(t => name.Contains(t))) return true;
        }

        return false;
    }
}
