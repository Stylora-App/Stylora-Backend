namespace Stylora.Application.DTOs;

public class TryOnRequest
{
    public string PersonImageBase64 { get; set; } = string.Empty;
    public string ClothingImageBase64 { get; set; } = string.Empty;
}

public class TryOnResponse
{
    public string GeneratedImage { get; set; } = string.Empty;
}
