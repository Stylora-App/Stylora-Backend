using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.Infrastructure.Services;

public class AsosService : IAsosService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string ApiHost = "asos2.p.rapidapi.com";
    private const string ApiBase = "https://asos2.p.rapidapi.com";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AsosService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<List<ShoppingProductDto>> SearchProductsAsync(string query, int limit, int offset)
    {
        var queryString = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "store",    "US" },
            { "country",  "US" },
            { "lang",     "en-US" },
            { "currency", "USD" },
            { "q",        query },
            { "limit",    limit.ToString() },
            { "offset",   offset.ToString() },
        }).ReadAsStringAsync().Result;

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/products/v2/list?{queryString}");
        request.Headers.Add("X-RapidAPI-Key", _apiKey);
        request.Headers.Add("X-RapidAPI-Host", ApiHost);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"ASOS API error ({(int)response.StatusCode}): {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AsosApiResponse>(JsonOptions);
        return (result?.Products ?? []).Select(MapToDto).ToList();
    }

    private static ShoppingProductDto MapToDto(AsosApiProduct p) => new()
    {
        Id       = p.Id,
        Name     = p.Name ?? string.Empty,
        BrandName = p.BrandName ?? "ASOS",
        Price    = p.Price?.Current?.Text ?? string.Empty,
        ImageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? string.Empty : $"https:{p.ImageUrl}",
        Url      = string.IsNullOrWhiteSpace(p.Url) ? "https://www.asos.com" : $"https://www.asos.com/{p.Url}",
        Colour   = p.Colour,
    };
}

// ── Raw API response models ──────────────────────────────────────────────────

internal class AsosApiResponse
{
    [JsonPropertyName("products")]
    public List<AsosApiProduct>? Products { get; set; }
}

internal class AsosApiProduct
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("price")]
    public AsosApiPrice? Price { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("colour")]
    public string? Colour { get; set; }
}

internal class AsosApiPrice
{
    [JsonPropertyName("current")]
    public AsosApiPriceCurrent? Current { get; set; }
}

internal class AsosApiPriceCurrent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
