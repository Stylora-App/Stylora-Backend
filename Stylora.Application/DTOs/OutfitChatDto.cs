namespace Stylora.Application.DTOs;

public class OutfitChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class OutfitChatRequest
{
    public List<OutfitChatMessageDto> Messages { get; set; } = [];
}

public class OutfitChatResponse
{
    public string Status { get; set; } = "follow_up";
    public string AssistantMessage { get; set; } = string.Empty;
    public List<string> MissingFields { get; set; } = [];
    public List<string> MissingRoles { get; set; } = [];
    public List<string> SuggestedReplies { get; set; } = [];
    public OutfitBoardDto? Outfit { get; set; }
}

public class OutfitBoardDto
{
    public string Occasion { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string WeatherSummary { get; set; } = string.Empty;
    public string Gender { get; set; } = "unisex";
    public string Summary { get; set; } = string.Empty;
    public List<string> Palette { get; set; } = [];
    public List<OutfitBoardItemDto> Items { get; set; } = [];
}

public class OutfitBoardItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? ArticleTypeLabel { get; set; }
    public string? Color { get; set; }
}
