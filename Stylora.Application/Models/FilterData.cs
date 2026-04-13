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
        "suspender", "nightwear", "pyjama", "pajama",
    ];

    /// <summary>Extra exclusions when the user selected <c>men</c>.</summary>
    public static readonly string[] ExcludeTermsMen =
    [
        "skirt", " dress", "blouse", " heels", "stiletto",
        "womens", "women's", "for her", "maternity",
        "bra ", "corset", "bodysuit",
    ];

    /// <summary>Extra exclusions when the user selected <c>women</c>.</summary>
    public static readonly string[] ExcludeTermsWomen =
    [
        "mens ", "men's ", "for him",
        "boxer", "jockstrap",
    ];
}
