namespace Stylora.Application.DTOs;

public class WardrobeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Style { get; set; }
    public string? Color { get; set; }
    public int WornCount { get; set; }
}

public class CreateWardrobeItemRequest
{
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Style { get; set; }
    public string? Color { get; set; }
}

public class UserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePicture { get; set; }
    public string? Style { get; set; }
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public List<string> Palette { get; set; } = [];
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePicture { get; set; }
    public string? Style { get; set; }
}
