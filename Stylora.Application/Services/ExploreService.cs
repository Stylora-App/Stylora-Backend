using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.Application.Services;

public class ExploreService : IExploreService
{
    private readonly IAsosService _asosService;

    // Always fetch a full ASOS page so palette filtering leaves enough results
    private const int AsosBatchSize = 48;

    // ── Named colors for hex → descriptive name lookup ───────────────────────
    // Ordered from specific to general so nearest-neighbor finds best match.
    private static readonly (string Name, byte R, byte G, byte B)[] NamedColours =
    [
        ("ivory",          255, 255, 240),
        ("cream",          255, 250, 210),
        ("off-white",      250, 248, 243),
        ("pearl",          234, 230, 224),
        ("white",          255, 255, 255),
        ("beige",          240, 232, 205),
        ("nude",           235, 205, 185),
        ("blush",          255, 175, 168),
        ("rose",           210,  90, 110),
        ("hot pink",       255, 105, 180),
        ("pink",           255, 185, 195),
        ("fuchsia",        220,  20, 147),
        ("magenta",        210,  25, 130),
        ("lavender",       225, 210, 250),
        ("lilac",          195, 155, 205),
        ("mauve",          185, 135, 175),
        ("purple",         128,   0, 130),
        ("plum",           130,  55,  95),
        ("violet",         225, 120, 225),
        ("coral",          255, 120,  80),
        ("salmon",         250, 128, 110),
        ("peach",          255, 205, 165),
        ("apricot",        250, 185, 135),
        ("terracotta",     185,  95,  68),
        ("rust",           175,  60,  15),
        ("burnt orange",   180,  75,  10),
        ("orange",         255, 140,   0),
        ("amber",          255, 175,  20),
        ("gold",           210, 170,  45),
        ("mustard",        200, 165,  50),
        ("yellow",         255, 220,  50),
        ("lemon",          255, 245,  80),
        ("sand",           195, 175, 125),
        ("camel",          190, 150, 100),
        ("tan",            205, 175, 135),
        ("wheat",          240, 218, 170),
        ("khaki",          190, 175, 140),
        ("sage",           175, 190, 158),
        ("mint",           165, 230, 195),
        ("olive",          115, 115,  35),
        ("green",           65, 155,  65),
        ("forest",          30,  80,  30),
        ("emerald",          0, 140,  75),
        ("teal",             0, 125, 120),
        ("turquoise",       50, 210, 195),
        ("powder blue",    175, 220, 235),
        ("sky blue",       130, 200, 230),
        ("periwinkle",     150, 150, 245),
        ("blue",            38, 115, 250),
        ("cobalt",           0,  68, 170),
        ("navy",             5,  10,  75),
        ("indigo",          65,   0, 130),
        ("stone",           150, 142, 128),
        ("taupe",           112,  92,  78),
        ("chocolate",       145,  75,  20),
        ("brown",           115,  55,  25),
        ("coffee",           75,  42,  18),
        ("walnut",           70,  38,  25),
        ("burgundy",         95,   8,  28),
        ("wine",            108,  40,  48),
        ("maroon",           95,  18,  18),
        ("crimson",         175,  12,  42),
        ("scarlet",         205,  22,  18),
        ("red",             200,  28,  28),
        ("cherry",          215,  45,  85),
        ("charcoal",         52,  65,  75),
        ("grey",            130, 130, 130),
        ("silver",          188, 188, 188),
        ("black",             8,   8,   8),
    ];

    // ── Category → search terms (women / default) ────────────────────────────
    private static readonly Dictionary<string, string> CategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "top blouse shirt" },
            { "bottoms",     "pants trousers skirt jeans" },
            { "dresses",     "dress" },
            { "shoes",       "shoes boots heels sneakers" },
            { "accessories", "accessories bag handbag jewelry" },
            { "outerwear",   "coat jacket blazer" },
        };

    // ── Category → search terms (men) ─────────────────────────────────────────
    private static readonly Dictionary<string, string> MenCategoryTerms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "tops",        "shirt t-shirt polo sweatshirt jumper" },
            { "bottoms",     "pants trousers jeans chinos shorts" },
            { "shoes",       "shoes boots sneakers trainers loafers" },
            { "accessories", "accessories watch belt bag backpack" },
            { "outerwear",   "coat jacket blazer puffer" },
        };

    // ── Extra exclusions for men ───────────────────────────────────────────────
    private static readonly string[] ExcludeTermsMen =
    [
        "skirt", " dress", "blouse", " heels", "stiletto", "womens",
        "women's", "for her",
    ];

    // ── Colour-family keyword matching for ASOS `colour` field ───────────────
    private static readonly Dictionary<string, string[]> ColourFamilyKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "red",       ["red", "scarlet", "crimson", "cherry", "wine", "claret", "ruby", "tomato"] },
            { "pink",      ["pink", "rose", "blush", "fuchsia", "magenta", "hot pink", "dusty pink", "candy", "flamingo"] },
            { "orange",    ["orange", "coral", "peach", "apricot", "salmon", "rust", "amber", "terracotta", "copper", "burnt orange", "papaya"] },
            { "yellow",    ["yellow", "gold", "mustard", "lemon", "cream", "straw", "butter", "wheat", "honey"] },
            { "green",     ["green", "mint", "olive", "sage", "khaki", "emerald", "forest", "jade", "hunter", "pistachio", "lime"] },
            { "teal",      ["teal", "turquoise", "aqua", "cyan", "jade green"] },
            { "blue",      ["blue", "navy", "cobalt", "royal", "sky", "denim", "indigo", "periwinkle", "powder", "ice blue", "steel blue", "cornflower"] },
            { "purple",    ["purple", "violet", "lavender", "lilac", "mauve", "plum", "grape", "aubergine", "eggplant"] },
            { "brown",     ["brown", "camel", "tan", "taupe", "chocolate", "coffee", "mocha", "walnut", "cocoa", "toffee", "cinnamon", "hazel"] },
            { "nude",      ["nude", "beige", "sand", "natural", "oatmeal", "ivory", "stone", "ecru", "biscuit", "blush beige", "latte", "caramel"] },
            { "grey",      ["grey", "gray", "silver", "charcoal", "steel", "ash", "slate", "fog", "smoke"] },
            { "black",     ["black", "ebony", "jet", "onyx", "midnight", "ink"] },
            { "white",     ["white", "off white", "off-white", "snow", "bright white", "optical white", "cream white"] },
        };

    // ── Products to never show ────────────────────────────────────────────────
    private static readonly string[] ExcludeTerms =
    [
        "lingerie", "bikini", "swimsuit", "swimwear", "swim",
        "bralette", "panties", "knickers", "thong", "g-string",
        "suspender", "nightwear", "pyjama", "pajama",
    ];

    // ── Fallback season queries (used only when no palette is available) ──────
    private static readonly Dictionary<string, string> SeasonFallback =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "light spring",  "coral peach blush yellow ivory" },
            { "true spring",   "orange yellow camel warm golden" },
            { "bright spring", "coral fuchsia yellow turquoise vivid" },
            { "light summer",  "lavender powder blue pink rose blush" },
            { "true summer",   "mauve rose grey blue soft muted" },
            { "soft summer",   "dusty pink mauve sage grey rose" },
            { "soft autumn",   "terracotta camel olive sage brown" },
            { "true autumn",   "rust orange olive gold brown warm" },
            { "dark autumn",   "burgundy wine chocolate forest olive" },
            { "dark winter",   "navy black burgundy charcoal ivory" },
            { "true winter",   "navy red black white fuchsia" },
            { "bright winter", "fuchsia cobalt black white royal" },
        };

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

        // 1. Strip lingerie / swimwear + gender-specific items
        var products = raw.Where(p => !IsExcluded(p, query.Gender)).ToList();

        // 2. Hard palette filter — only return colour-matched items
        var families = BuildPaletteColorFamilies(query.Palette);
        if (families.Count > 0)
        {
            products = products.Where(p => IsColourMatch(p.Colour, families)).ToList();
            foreach (var p in products) p.PaletteMatch = true;
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

        // User typed an explicit search — honour it, add gender context
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            return $"{gender} {query.Q} {category}".Trim();
        }

        // Derive colour terms from actual palette hex codes (most accurate)
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
        if (isMen && MenCategoryTerms.TryGetValue(category, out var menTerm))
            return menTerm;

        return CategoryTerms.GetValueOrDefault(category, "fashion");
    }

    private static string GetSeasonFallback(string? season)
    {
        if (string.IsNullOrWhiteSpace(season)) return "fashion outfit trending";
        var lower = season.ToLowerInvariant();
        var match = SeasonFallback.Keys.FirstOrDefault(k => lower.Contains(k));
        return match is not null ? SeasonFallback[match] : "fashion outfit trending";
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

            return NamedColours
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

    private HashSet<string> BuildPaletteColorFamilies(List<string>? palette)
    {
        if (palette is null || palette.Count == 0) return [];

        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hex in palette)
        {
            var name = HexToColorName(hex);
            if (name is null) continue;

            // Map named color → family (find which family's keywords the name belongs to)
            foreach (var (family, keywords) in ColourFamilyKeywords)
            {
                if (keywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    families.Add(family);
                    break;
                }
            }

            // Direct name-as-family fallback (e.g. "teal" maps to "teal" family)
            if (ColourFamilyKeywords.ContainsKey(name)) families.Add(name);
        }
        return families;
    }

    private static bool IsColourMatch(string? colour, HashSet<string> families)
    {
        if (string.IsNullOrWhiteSpace(colour)) return false;
        var lower = colour.ToLowerInvariant();
        return families.Any(family =>
            ColourFamilyKeywords.TryGetValue(family, out var kws) &&
            kws.Any(kw => lower.Contains(kw)));
    }

    // ── Exclusion filter ──────────────────────────────────────────────────────

    private static bool IsExcluded(ShoppingProductDto p, string? gender)
    {
        var name = p.Name.ToLowerInvariant();
        if (ExcludeTerms.Any(t => name.Contains(t))) return true;

        if (gender?.Equals("men", StringComparison.OrdinalIgnoreCase) == true)
            if (ExcludeTermsMen.Any(t => name.Contains(t))) return true;

        return false;
    }
}
