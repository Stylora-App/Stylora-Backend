namespace Stylora.Domain.Entities;

/// <summary>
/// Normalized color entity for 3NF compliance - avoids storing color names as strings in multiple places
/// </summary>
public class Color
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    
    // Navigation properties
    public ICollection<WardrobeItem> WardrobeItems { get; set; } = [];
    public ICollection<RecommendedColor> RecommendedColors { get; set; } = [];
    public ICollection<UserPaletteColor> UserPaletteColors { get; set; } = [];
}
