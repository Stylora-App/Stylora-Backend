namespace Stylora.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between WardrobeItem and Tag (3NF)
/// </summary>
public class WardrobeItemTag
{
    public Guid WardrobeItemId { get; set; }
    public WardrobeItem WardrobeItem { get; set; } = null!;
    
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
