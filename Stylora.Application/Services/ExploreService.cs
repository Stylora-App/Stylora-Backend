using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;

namespace Stylora.Application.Services;

public class ExploreService : IExploreService
{
    private readonly IAsosService _asosService;
    private readonly IMemoryCache _cache;

    private const int AsosBatchSize = 48;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly Regex TokenSplitRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ExploreService(IAsosService asosService, IMemoryCache? cache = null)
    {
        _asosService = asosService;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<ExploreResultDto> SearchAsync(ExploreQueryDto query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, AsosBatchSize);
        var page = Math.Max(query.Page, 1);
        var filteredQuery = BuildFilteredQuery(query);
        var cacheKey = BuildCacheKey(filteredQuery);
        var neededCount = (page * pageSize) + 1;
        var cacheEntry = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SetSlidingExpiration(CacheDuration);
            return new ExploreCacheEntry();
        })!;

        await cacheEntry.Gate.WaitAsync();
        try
        {
            while (!cacheEntry.Exhausted && cacheEntry.Products.Count < neededCount)
            {
                var raw = await _asosService.SearchProductsAsync(filteredQuery.BrowseQuery, AsosBatchSize, cacheEntry.NextOffset);
                cacheEntry.NextOffset += AsosBatchSize;

                if (raw.Count < AsosBatchSize)
                {
                    cacheEntry.Exhausted = true;
                }

                foreach (var product in ApplyFilters(raw, filteredQuery))
                {
                    cacheEntry.Products.Add(product);
                }
            }
        }
        finally
        {
            cacheEntry.Gate.Release();
        }

        var skipCount = (page - 1) * pageSize;
        var pageProducts = cacheEntry.Products
            .Skip(skipCount)
            .Take(pageSize)
            .Select(CloneProduct)
            .ToList();

        return new ExploreResultDto
        {
            Products = pageProducts,
            HasMore = cacheEntry.Products.Count > skipCount + pageSize || !cacheEntry.Exhausted,
            Page = page,
            PageSize = pageSize,
        };
    }

    private static FilteredExploreQuery BuildFilteredQuery(ExploreQueryDto query)
    {
        var browseQuery = BuildBrowseQuery(query);
        var paletteFamilies = BuildPaletteFamilies(query.Season, query.SubSeason, query.Palette);
        var paletteFamiliesWithNeutrals = BuildFamiliesWithNeutrals(paletteFamilies);

        return new FilteredExploreQuery(
            browseQuery,
            query.Q,
            query.Category,
            query.Gender,
            query.Season,
            query.SubSeason,
            paletteFamilies,
            paletteFamiliesWithNeutrals,
            BuildQueryTokenGroups(query.Q));
    }

    private static string BuildCacheKey(FilteredExploreQuery query)
    {
        var families = query.PaletteFamilies.Count == 0
            ? "none"
            : string.Join(',', query.PaletteFamilies.OrderBy(static family => family));

        return string.Join('|',
        [
            query.BrowseQuery,
            query.SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty,
            query.Gender?.Trim().ToLowerInvariant() ?? string.Empty,
            query.Category?.Trim().ToLowerInvariant() ?? "all",
            query.Season?.Trim().ToLowerInvariant() ?? string.Empty,
            query.SubSeason?.Trim().ToLowerInvariant() ?? string.Empty,
            families,
        ]);
    }

    private static string BuildBrowseQuery(ExploreQueryDto query)
    {
        var gender = GenderTerm(query.Gender);
        var category = BuildCategoryTerm(query.Category, query.Gender);
        return $"{gender} {category}".Trim();
    }

    private static string GenderTerm(string? gender) => gender?.ToLowerInvariant() switch
    {
        "women" => "women",
        "men" => "men",
        _ => string.Empty,
    };

    private static string BuildCategoryTerm(string? category, string? gender)
    {
        if (string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return "fashion outfit";
        }

        var isMen = gender?.Equals("men", StringComparison.OrdinalIgnoreCase) == true;
        if (isMen && CategoryData.MenCategoryTerms.TryGetValue(category, out var menTerm))
        {
            return menTerm;
        }

        return CategoryData.WomenCategoryTerms.GetValueOrDefault(category, "fashion");
    }

    private static HashSet<string> BuildPaletteFamilies(string? season, string? subSeason, List<string>? palette)
    {
        if (SeasonData.TryGetSeasonVector(season, subSeason, out var seasonVector))
        {
            return new HashSet<string>(seasonVector.ColourFamilies, StringComparer.OrdinalIgnoreCase);
        }

        return BuildPaletteColorFamilies(palette);
    }

    private static HashSet<string> BuildPaletteColorFamilies(List<string>? palette)
    {
        if (palette is null || palette.Count == 0)
        {
            return [];
        }

        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hex in palette)
        {
            var name = HexToColorName(hex);
            if (name is null)
            {
                continue;
            }

            foreach (var (family, keywords) in ColourData.ColourFamilyKeywords)
            {
                if (keywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    families.Add(family);
                    break;
                }
            }

            if (ColourData.ColourFamilyKeywords.ContainsKey(name))
            {
                families.Add(name);
            }
        }

        return families;
    }

    private static HashSet<string>? BuildFamiliesWithNeutrals(HashSet<string> families)
    {
        if (families.Count == 0)
        {
            return null;
        }

        var withNeutrals = new HashSet<string>(families, StringComparer.OrdinalIgnoreCase);
        foreach (var neutralFamily in ColourData.NeutralFamilies)
        {
            withNeutrals.Add(neutralFamily);
        }

        return withNeutrals;
    }

    private static IEnumerable<ShoppingProductDto> ApplyFilters(
        IEnumerable<ShoppingProductDto> products,
        FilteredExploreQuery query)
    {
        foreach (var product in products)
        {
            if (IsExcluded(product, query.Gender))
            {
                continue;
            }

            if (query.PaletteFamiliesWithNeutrals is not null)
            {
                if (!IsColourMatch(product.Colour, query.PaletteFamiliesWithNeutrals))
                {
                    continue;
                }

                product.PaletteMatch = IsColourMatch(product.Colour, query.PaletteFamilies);
            }
            else
            {
                product.PaletteMatch = false;
            }

            if (!MatchesSearchQuery(product, query.QueryTokenGroups))
            {
                continue;
            }

            yield return CloneProduct(product);
        }
    }

    private static bool MatchesSearchQuery(ShoppingProductDto product, IReadOnlyList<HashSet<string>> queryTokenGroups)
    {
        if (queryTokenGroups.Count == 0)
        {
            return true;
        }

        var haystack = string.Join(' ', [product.Name, product.BrandName, product.Colour ?? string.Empty]).ToLowerInvariant();
        return queryTokenGroups.All(group => group.Any(alias => haystack.Contains(alias, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<HashSet<string>> BuildQueryTokenGroups(string? searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return [];
        }

        return TokenSplitRegex
            .Split(searchQuery.ToLowerInvariant())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Select(ExpandSearchToken)
            .ToList();
    }

    private static HashSet<string> ExpandSearchToken(string token)
    {
        var normalized = NormalizeToken(token);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalized };

        if (FilterData.SearchAliases.TryGetValue(normalized, out var aliases))
        {
            expanded.UnionWith(aliases);
        }

        foreach (var (canonical, relatedTerms) in FilterData.SearchAliases)
        {
            if (canonical.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                relatedTerms.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                expanded.Add(canonical);
                expanded.UnionWith(relatedTerms);
            }
        }

        foreach (var (family, keywords) in ColourData.ColourFamilyKeywords)
        {
            if (family.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                keywords.Any(keyword => keyword.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                expanded.Add(family);
                expanded.UnionWith(keywords);
            }
        }

        if (normalized.EndsWith('s') && normalized.Length > 3)
        {
            expanded.Add(normalized[..^1]);
        }
        else if (!normalized.EndsWith('s'))
        {
            expanded.Add($"{normalized}s");
        }

        return expanded;
    }

    private static string NormalizeToken(string token)
        => token.Trim().Trim('\'', '"').ToLowerInvariant();

    private static bool IsColourMatch(string? colour, HashSet<string> families)
    {
        if (string.IsNullOrWhiteSpace(colour))
        {
            return false;
        }

        var lower = colour.ToLowerInvariant();
        return families.Any(family =>
            ColourData.ColourFamilyKeywords.TryGetValue(family, out var keywords) &&
            keywords.Any(keyword => lower.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? HexToColorName(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return null;
        }

        try
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);

            return ColourData.NamedColours
                .OrderBy(colour => RgbDistSq(r, g, b, colour.R, colour.G, colour.B))
                .First()
                .Name;
        }
        catch
        {
            return null;
        }
    }

    private static int RgbDistSq(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return dr * dr + dg * dg + db * db;
    }

    private static bool IsExcluded(ShoppingProductDto product, string? gender)
    {
        var name = product.Name.ToLowerInvariant();
        if (FilterData.ExcludeTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (gender?.Equals("men", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (FilterData.ExcludeTermsMen.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        else if (gender?.Equals("women", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (FilterData.ExcludeTermsWomen.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static ShoppingProductDto CloneProduct(ShoppingProductDto product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        BrandName = product.BrandName,
        Price = product.Price,
        ImageUrl = product.ImageUrl,
        Url = product.Url,
        Colour = product.Colour,
        PaletteMatch = product.PaletteMatch,
    };

    private sealed record FilteredExploreQuery(
        string BrowseQuery,
        string? SearchQuery,
        string? Category,
        string? Gender,
        string? Season,
        string? SubSeason,
        HashSet<string> PaletteFamilies,
        HashSet<string>? PaletteFamiliesWithNeutrals,
        IReadOnlyList<HashSet<string>> QueryTokenGroups);

    private sealed class ExploreCacheEntry
    {
        public List<ShoppingProductDto> Products { get; } = [];
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int NextOffset { get; set; }
        public bool Exhausted { get; set; }
    }
}
