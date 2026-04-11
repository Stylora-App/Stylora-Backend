using Stylora.Domain.Enums;

namespace Stylora.Domain.Entities;

public class User : BaseEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePicture { get; set; }
    public StylePreference? Style { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public SeasonAnalysisResult? ColorAnalysisResult { get; set; }
    public ICollection<WardrobeItem> WardrobeItems { get; set; } = [];
    public ICollection<TryOnSession> TryOnSessions { get; set; } = [];
}
