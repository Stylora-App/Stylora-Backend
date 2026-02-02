namespace Stylora.Domain.Entities;

/// <summary>
/// Entity to track when items are worn - normalized from WardrobeItem (3NF)
/// Allows tracking history instead of just count
/// </summary>
public class WearLog
{
    public Guid Id { get; set; }
    public Guid WardrobeItemId { get; set; }
    public WardrobeItem WardrobeItem { get; set; } = null!;
    
    public DateTime WornAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? Occasion { get; set; }
    
    // Optional link to outfit if worn as part of an outfit
    public Guid? OutfitSuggestionId { get; set; }
    public OutfitSuggestion? OutfitSuggestion { get; set; }
}
