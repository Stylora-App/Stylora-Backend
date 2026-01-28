namespace Stylora.Application.DTOs;

public class SeasonAnalysisRequest
{
    public string ImageBase64 { get; set; } = string.Empty;
}

public class SeasonAnalysisResponse
{
    public string Season { get; set; } = string.Empty;
    public string SubSeason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RecommendedColors { get; set; } = [];
    public string BestMetals { get; set; } = string.Empty;
}
