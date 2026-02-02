namespace Stylora.Domain.Entities;

public class OutfitSuggestion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string Reasoning { get; set; } = string.Empty;
    public string StyleTip { get; set; } = string.Empty;
    public string? Occasion { get; set; }
    public string? Weather { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsFavorite { get; set; }
    public int? Rating { get; set; }
    
    // Navigation property for outfit items (3NF - allows multiple items per outfit)
    public ICollection<OutfitItem> OutfitItems { get; set; } = [];
    public ICollection<WearLog> WearLogs { get; set; } = [];
}
