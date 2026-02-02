namespace Stylora.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string? DisplayName { get; set; }
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public string? AvatarPath { get; set; }
    public string? PreferredStyle { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property for palette colors (3NF many-to-many)
    public ICollection<UserPaletteColor> PaletteColors { get; set; } = [];
}
