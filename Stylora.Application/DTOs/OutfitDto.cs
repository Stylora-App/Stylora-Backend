namespace Stylora.Application.DTOs;

public class OutfitSuggestionRequest
{
    public List<WardrobeItemDto> Items { get; set; } = [];
    public string Occasion { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
}

public class OutfitSuggestionResponse
{
    public string? Id { get; set; }
    public string? TopId { get; set; }
    public string? BottomId { get; set; }
    public string? ShoeId { get; set; }
    public string? Occasion { get; set; }
    public string? Weather { get; set; }
    public string? Reasoning { get; set; }
    public string? StyleTip { get; set; }
}
