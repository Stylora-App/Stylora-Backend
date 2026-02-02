namespace Stylora.Domain.Entities;

/// <summary>
/// Normalized tag entity for 3NF compliance
/// </summary>
public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    // Navigation property for many-to-many relationship
    public ICollection<WardrobeItemTag> WardrobeItemTags { get; set; } = [];
}
