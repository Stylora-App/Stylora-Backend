namespace Stylora.Application.DTOs;

public class WardrobeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Style { get; set; }
    public string? Color { get; set; }
    public int WornCount { get; set; }
    public string? ValidationStatus { get; set; }
    public double? ValidationConfidence { get; set; }
    public string? ValidationMessage { get; set; }
    public DateTime? ValidatedAt { get; set; }
}

public class CreateWardrobeItemRequest
{
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Style { get; set; }
    public string? Color { get; set; }
    public bool OverrideValidationWarning { get; set; }
}

public class WardrobeValidationDto
{
    public string Status { get; set; } = "warning";
    public bool IsLikelyClothing { get; set; }
    public double Confidence { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool CanOverride { get; set; }
    public List<string> NearestLabels { get; set; } = [];
}

public class CreateWardrobeItemResponse
{
    public WardrobeItemDto? Item { get; set; }
    public WardrobeValidationDto? Validation { get; set; }
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
