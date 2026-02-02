namespace Stylora.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between UserProfile and Color (3NF)
/// </summary>
public class UserPaletteColor
{
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    
    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;
    
    public int DisplayOrder { get; set; }
}
