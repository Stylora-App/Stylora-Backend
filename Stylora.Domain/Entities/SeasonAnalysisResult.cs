namespace Stylora.Domain.Entities;

public class SeasonAnalysisResult
{
    public string Season { get; set; } = string.Empty;
    public string SubSeason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RecommendedColors { get; set; } = [];
    public string BestMetals { get; set; } = string.Empty;
}
