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
        var asosOffset = (Math.Max(query.Page, 1) - 1) * AsosBatchSize;

        var searchTerm = BuildQuery(query);
        var raw = await _asosService.SearchProductsAsync(searchTerm, AsosBatchSize, asosOffset);

        var products = raw.Where(p => !IsExcluded(p, query.Gender)).ToList();

        // Palette filter — match colour families + always allow neutrals for diversity
        var families = BuildPaletteColorFamilies(query.Palette);
        if (families.Count > 0)
        {
            var withNeutrals = new HashSet<string>(families, StringComparer.OrdinalIgnoreCase);
            foreach (var n in ColourData.NeutralFamilies)
                withNeutrals.Add(n);

            products = products.Where(p => IsColourMatch(p.Colour, withNeutrals)).ToList();

            foreach (var p in products)
                p.PaletteMatch = IsColourMatch(p.Colour, families);
        }

        return new ExploreResultDto
        {
            Products = products.Take(pageSize).ToList(),
            HasMore  = raw.Count == AsosBatchSize,
            Page     = query.Page,
            PageSize = pageSize,
        };
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    private static string BuildQuery(ExploreQueryDto query)
    {
        var gender   = GenderTerm(query.Gender);
        var category = BuildCategoryTerm(query.Category, query.Gender);

        if (!string.IsNullOrWhiteSpace(query.Q))
            return $"{gender} {query.Q} {category}".Trim();

        string colourTerms;
        if (query.Palette is { Count: > 0 })
        {
            colourTerms = string.Join(" ",
                query.Palette
                    .Select(HexToColorName)
                    .Where(n => n is not null)
                    .Distinct()
                    .Take(6));
        }
        else
        {
            colourTerms = GetSeasonFallback(query.Season);
        }

        return $"{colourTerms} {gender} {category}".Trim();
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

    private static string GetSeasonFallback(string? season)
    {
        if (string.IsNullOrWhiteSpace(season)) return "fashion outfit trending";
        var lower = season.ToLowerInvariant();
        var match = SeasonData.SeasonFallback.Keys.FirstOrDefault(k => lower.Contains(k));
        return match is not null ? SeasonData.SeasonFallback[match] : "fashion outfit trending";
    }

    // ── Hex → nearest named colour ────────────────────────────────────────────

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

    private static bool IsColourMatch(string? colour, HashSet<string> families)
    {
        if (string.IsNullOrWhiteSpace(colour)) return false;
        var lower = colour.ToLowerInvariant();
        return families.Any(family =>
            ColourData.ColourFamilyKeywords.TryGetValue(family, out var kws) &&
            kws.Any(kw => lower.Contains(kw)));
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
