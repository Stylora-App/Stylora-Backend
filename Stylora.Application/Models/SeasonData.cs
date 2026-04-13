namespace Stylora.Application.Models;

/// <summary>
/// Fallback colour-term queries keyed by armochromia season.
/// Used only when the user has no explicit palette hex codes.
/// </summary>
public static class SeasonData
{
    public static readonly Dictionary<string, string> SeasonFallback =
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
}
