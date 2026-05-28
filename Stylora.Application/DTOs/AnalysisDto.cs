namespace Stylora.Application.DTOs;

public class SeasonAnalysisRequest
{
    public string ImageBase64 { get; set; } = string.Empty;
}

public class SeasonAnalysisResponse
{
    public string? Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public string SubSeason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RecommendedColors { get; set; } = [];
    public string BestMetals { get; set; } = string.Empty;

    // Mind-map annotation data (only populated on fresh Gemini analysis)
    public string? HairColor { get; set; }
    public string? HairDetail { get; set; }
    public string? EyeColor { get; set; }
    public string? EyeDetail { get; set; }
    public string? SkinTone { get; set; }
    public string? SkinDetail { get; set; }
    public string? Undertone { get; set; }
    public string? Contrast { get; set; }
}
