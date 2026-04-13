namespace Stylora.Application.Models;

/// <summary>
/// Named-colour lookup table and colour-family keyword sets used for
/// palette matching in the Explore feature.
/// </summary>
public static class ColourData
{
    /// <summary>
    /// Reference palette of named colours with their RGB values.
    /// Ordered from specific to general so nearest-neighbour finds the best match.
    /// </summary>
    public static readonly (string Name, byte R, byte G, byte B)[] NamedColours =
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
        ("stone",          150, 142, 128),
        ("taupe",          112,  92,  78),
        ("chocolate",      145,  75,  20),
        ("brown",          115,  55,  25),
        ("coffee",          75,  42,  18),
        ("walnut",          70,  38,  25),
        ("burgundy",        95,   8,  28),
        ("wine",           108,  40,  48),
        ("maroon",          95,  18,  18),
        ("crimson",        175,  12,  42),
        ("scarlet",        205,  22,  18),
        ("red",            200,  28,  28),
        ("cherry",         215,  45,  85),
        ("charcoal",        52,  65,  75),
        ("grey",           130, 130, 130),
        ("silver",         188, 188, 188),
        ("black",            8,   8,   8),
    ];

    /// <summary>
    /// Maps a colour-family name to keywords that may appear in the ASOS
    /// <c>colour</c> field.  Used for post-fetch palette filtering.
    /// </summary>
    public static readonly Dictionary<string, string[]> ColourFamilyKeywords =
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

    /// <summary>
    /// Colour families that are wardrobe-neutral and should always pass
    /// through the palette filter regardless of the user's seasonal palette.
    /// </summary>
    public static readonly HashSet<string> NeutralFamilies =
        new(StringComparer.OrdinalIgnoreCase) { "black", "white", "grey", "nude" };
}
