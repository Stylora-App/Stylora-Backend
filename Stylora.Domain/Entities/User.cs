namespace Stylora.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public UserProfile? Profile { get; set; }
    public ICollection<WardrobeItem> WardrobeItems { get; set; } = [];
    public ICollection<SeasonAnalysisResult> SeasonAnalyses { get; set; } = [];
    public ICollection<OutfitSuggestion> OutfitSuggestions { get; set; } = [];
    public ICollection<TryOnSession> TryOnSessions { get; set; } = [];
}
