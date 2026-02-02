namespace Stylora.Domain.Entities;

public class WardrobeItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string ImagePath { get; set; } = string.Empty;
    public ClothingCategory Category { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    
    // Reference to normalized Color entity (3NF)
    public Guid? ColorId { get; set; }
    public Color? Color { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<WardrobeItemTag> WardrobeItemTags { get; set; } = [];
    public ICollection<WearLog> WearLogs { get; set; } = [];
    public ICollection<TryOnSession> TryOnSessions { get; set; } = [];
    
    // Outfit references (items can be part of multiple outfits)
    public ICollection<OutfitItem> OutfitItems { get; set; } = [];
}

public enum ClothingCategory
{
    Top,
    Bottom,
    Shoes,
    Accessory,
    FullBody,
    Outerwear,
    Bag
}
