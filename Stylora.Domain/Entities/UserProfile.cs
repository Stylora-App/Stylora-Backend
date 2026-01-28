namespace Stylora.Domain.Entities;

public class UserProfile
{
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public List<string> Palette { get; set; } = [];
    public string? Name { get; set; }
}
