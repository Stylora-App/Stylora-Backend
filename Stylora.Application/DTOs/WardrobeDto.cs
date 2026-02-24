namespace Stylora.Application.DTOs;

public class WardrobeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string? Color { get; set; }
    public string? Brand { get; set; }
    public int WearCount { get; set; }
    public string? LastWorn { get; set; }
    public string? Description { get; set; }
}

public class CreateWardrobeItemRequest
{
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string? Color { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
}

public class UserProfileDto
{
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public List<string> Palette { get; set; } = [];
    public string? DisplayName { get; set; }
    public string? PreferredStyle { get; set; }
    public string? ProfilePicture { get; set; }
}
