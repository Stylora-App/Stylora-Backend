namespace Stylora.Application.Models;

public sealed record SeasonVector(
    string Key,
    IReadOnlyList<string> PaletteHexes,
    IReadOnlySet<string> ColourFamilies);

public static class SeasonData
{
    private static readonly Dictionary<string, string> SeasonAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["spring"] = "true spring",
            ["warm spring"] = "true spring",
            ["light spring"] = "light spring",
            ["bright spring"] = "bright spring",
            ["clear spring"] = "bright spring",
            ["summer"] = "true summer",
            ["cool summer"] = "true summer",
            ["true summer"] = "true summer",
            ["light summer"] = "light summer",
            ["soft summer"] = "soft summer",
            ["muted summer"] = "soft summer",
            ["autumn"] = "true autumn",
            ["fall"] = "true autumn",
            ["warm autumn"] = "true autumn",
            ["true autumn"] = "true autumn",
            ["soft autumn"] = "soft autumn",
            ["dark autumn"] = "dark autumn",
            ["deep autumn"] = "dark autumn",
            ["winter"] = "true winter",
            ["cool winter"] = "true winter",
            ["true winter"] = "true winter",
            ["dark winter"] = "dark winter",
            ["deep winter"] = "dark winter",
            ["bright winter"] = "bright winter",
            ["clear winter"] = "bright winter",
        };

    public static readonly Dictionary<string, SeasonVector> Vectors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["light spring"] = new(
                "light spring",
                ["#F7C8A5", "#FCE7B2", "#BEE8D2", "#A9D6F5", "#F7B7C3", "#FFF4D6", "#CFE8A9", "#87C9C3"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pink", "orange", "yellow", "green", "teal", "blue", "nude", "white" }),
            ["true spring"] = new(
                "true spring",
                ["#FFB347", "#FFC857", "#F28C28", "#FF6F61", "#98D8AA", "#3CB371", "#00A6A6", "#F4E1A1"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "orange", "yellow", "green", "teal", "blue", "brown", "nude", "white" }),
            ["bright spring"] = new(
                "bright spring",
                ["#FF5E5B", "#FFB703", "#00C2A8", "#00A6FB", "#FF4FA3", "#7C4DFF", "#F9F871", "#FFFFFF"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pink", "orange", "yellow", "green", "teal", "blue", "purple", "white", "black" }),
            ["light summer"] = new(
                "light summer",
                ["#D8C3E8", "#A7C7E7", "#F4C2C2", "#CDE7D8", "#BFD7EA", "#F7E7CE", "#9FBAD6", "#E8D7FF"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pink", "purple", "blue", "teal", "grey", "white", "nude" }),
            ["true summer"] = new(
                "true summer",
                ["#B7C9E2", "#C9A0DC", "#D8A7B1", "#8FA3BF", "#A8C3BC", "#E5E0D8", "#7F8FA6", "#C4B7CB"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pink", "purple", "blue", "teal", "grey", "white" }),
            ["soft summer"] = new(
                "soft summer",
                ["#C7B8A3", "#A8B5A2", "#B8A9C9", "#D4B2A7", "#8FA39B", "#B7A6A1", "#D8D3C9", "#7E8A87"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pink", "purple", "blue", "green", "brown", "grey", "nude" }),
            ["soft autumn"] = new(
                "soft autumn",
                ["#C68642", "#A3B18A", "#B08968", "#D4A373", "#8D6E63", "#E6CCB2", "#6B705C", "#C9A227"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "orange", "yellow", "green", "brown", "nude" }),
            ["true autumn"] = new(
                "true autumn",
                ["#B85C38", "#D97D54", "#A3A847", "#6B8E23", "#C49A44", "#8C4A2F", "#E3B778", "#F2E6CE"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "red", "orange", "yellow", "green", "brown", "nude" }),
            ["dark autumn"] = new(
                "dark autumn",
                ["#6B2D1A", "#7B3F61", "#556B2F", "#2F4F3E", "#8B5A2B", "#C2A15A", "#3E2723", "#E6D3A3"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "red", "orange", "green", "brown", "black", "nude" }),
            ["dark winter"] = new(
                "dark winter",
                ["#1C1F33", "#4B1D3F", "#7A1E48", "#0F4C5C", "#2C3E50", "#E6E6E6", "#5B6670", "#B0C4DE"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "red", "pink", "blue", "purple", "black", "grey", "white" }),
            ["true winter"] = new(
                "true winter",
                ["#003366", "#C1121F", "#000000", "#FFFFFF", "#0066CC", "#D7263D", "#5F4B8B", "#B8C0FF"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "red", "pink", "blue", "purple", "black", "grey", "white" }),
            ["bright winter"] = new(
                "bright winter",
                ["#0047AB", "#FF007F", "#00C2FF", "#FF3131", "#000000", "#FFFFFF", "#7F00FF", "#00B894"],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "red", "pink", "blue", "purple", "black", "white" }),
        };

    public static SeasonVector? GetSeasonVector(string? season, string? subSeason)
    {
        if (TryGetSeasonVector(season, subSeason, out var vector))
        {
            return vector;
        }

        return null;
    }

    public static bool TryGetSeasonVector(string? season, string? subSeason, out SeasonVector vector)
    {
        foreach (var candidate in new[] { subSeason, season })
        {
            var normalized = NormalizeKey(candidate);
            if (normalized is not null && Vectors.TryGetValue(normalized, out vector!))
            {
                return true;
            }
        }

        vector = null!;
        return false;
    }

    public static List<string> GetPalette(string? season, string? subSeason)
        => GetSeasonVector(season, subSeason)?.PaletteHexes.ToList() ?? [];

    private static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            value.Trim()
                 .ToLowerInvariant()
                 .Replace('-', ' ')
                 .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return SeasonAliases.GetValueOrDefault(normalized, normalized);
    }
}
