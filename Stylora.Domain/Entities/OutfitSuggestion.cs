namespace Stylora.Domain.Entities;

public class OutfitSuggestion
{
    public string? TopId { get; set; }
    public string? BottomId { get; set; }
    public string? ShoeId { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string StyleTip { get; set; } = string.Empty;
}
