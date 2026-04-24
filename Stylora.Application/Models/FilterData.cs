namespace Stylora.Application.Models;

/// <summary>
/// Keyword exclusion lists for product filtering.
/// </summary>
public static class FilterData
{
    /// <summary>Products to never show regardless of gender.</summary>
    public static readonly string[] ExcludeTerms =
    [
        "lingerie", "bikini", "swimsuit", "swimwear", "swim",
        "bralette", "panties", "knickers", "thong", "g-string",
        "suspender", "nightwear", "pyjama", "pajama", "bra", "brief",
        "briefs", "underwear", "intimates", "sock", "socks", "tight",
        "tights", "stocking", "stockings", "shapewear", "set"
    ];

    /// <summary>Extra exclusions when the user selected <c>men</c>.</summary>
    public static readonly string[] ExcludeTermsMen =
    [
        "skirt", " dress", "blouse", " heels", "stiletto",
        "womens", "women's", "for her", "maternity",
        "bra ", "corset", "bodysuit", "bra-top", "bralet",
    ];

    /// <summary>Extra exclusions when the user selected <c>women</c>.</summary>
    public static readonly string[] ExcludeTermsWomen =
    [
        "mens ", "men's ", "for him",
        "boxer", "jockstrap", "jock strap",
    ];

    public static readonly Dictionary<string, string[]> SearchAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tee"] = ["tee", "tees", "t-shirt", "tshirt", "t shirt", "top"],
            ["shirt"] = ["shirt", "shirts", "blouse", "blouses", "button-down", "button down", "oxford"],
            ["jumper"] = ["jumper", "jumpers", "sweater", "sweaters", "knit", "knits", "knitted", "pullover"],
            ["hoodie"] = ["hoodie", "hoodies", "sweatshirt", "sweatshirts"],
            ["coat"] = ["coat", "coats", "jacket", "jackets", "trench", "trenchcoat", "parka", "puffer", "blazer", "blazers", "shacket"],
            ["dress"] = ["dress", "dresses", "gown", "gowns", "midaxi", "maxi", "midi", "mini"],
            ["trousers"] = ["trousers", "trouser", "pants", "pant", "slacks", "chinos", "chino"],
            ["jeans"] = ["jeans", "jean", "denim"],
            ["trainers"] = ["trainers", "trainer", "sneakers", "sneaker", "kicks", "runners"],
            ["boots"] = ["boots", "boot", "chelsea", "ankle boot", "ankle boots"],
            ["bag"] = ["bag", "bags", "handbag", "handbags", "tote", "totes", "satchel", "satchels", "crossbody", "cross-body", "purse", "backpack"],
            ["jewelry"] = ["jewelry", "jewellery", "earrings", "necklace", "bracelet", "ring"],
            ["formal"] = ["formal", "occasion", "tailored", "smart", "elegant"],
            ["casual"] = ["casual", "everyday", "relaxed", "easy"],
        };
}
