namespace Stylora.Application.DTOs;

public class ShoppingProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Colour { get; set; }
    public bool PaletteMatch { get; set; }
}

public class ExploreResultDto
{
    public List<ShoppingProductDto> Products { get; set; } = [];
    public bool HasMore { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ExploreQueryDto
{
    public string? Q { get; set; }
    public string? Category { get; set; }
    /// <summary>e.g. "women" | "men"</summary>
    public string? Gender { get; set; }
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public List<string>? Palette { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
