namespace Stylora.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between OutfitSuggestion and WardrobeItem (3NF)
/// Allows an outfit to have multiple items and items to be in multiple outfits
/// </summary>
public class OutfitItem
{
    public Guid OutfitSuggestionId { get; set; }
    public OutfitSuggestion OutfitSuggestion { get; set; } = null!;
    
    public Guid WardrobeItemId { get; set; }
    public WardrobeItem WardrobeItem { get; set; } = null!;
    
    /// <summary>
    /// Role of the item in the outfit (e.g., "Top", "Bottom", "Shoes", "Accessory")
    /// </summary>
    public string ItemRole { get; set; } = string.Empty;
    
    public int DisplayOrder { get; set; }
}
