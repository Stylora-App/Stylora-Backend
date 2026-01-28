namespace Stylora.Domain.Entities;

public class WardrobeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Image { get; set; } = string.Empty; // Base64
    public ClothingCategory Category { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? Color { get; set; }
    public int WearCount { get; set; }
    public DateTime? LastWorn { get; set; }
    public string? Description { get; set; }
}

public enum ClothingCategory
{
    Top,
    Bottom,
    Shoes,
    Accessory,
    FullBody
}
