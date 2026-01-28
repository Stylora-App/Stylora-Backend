using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stylora.Infrastructure.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    public GeminiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    private async Task<string> GenerateContentAsync(string prompt, string? imageBase64 = null, string model = "gemini-2.0-flash")
    {
        var url = $"{BaseUrl}/models/{model}:generateContent?key={_apiKey}";

        var parts = new List<object> { new { text = prompt } };
        
        if (!string.IsNullOrEmpty(imageBase64))
        {
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/jpeg",
                    data = imageBase64
                }
            });
        }

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = parts
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "";
    }

    public async Task<SeasonAnalysisResult> AnalyzeSeasonAsync(string imageBase64)
    {
        var prompt = @"Analyze this person's skin undertone, eye color, and hair color strictly according to Armochromia theory. 
        Determine their seasonal color palette (Spring, Summer, Autumn, Winter) and specific sub-season (e.g., Deep Autumn, Light Summer).
        Provide a list of recommended colors (hex codes) and a brief explanation.
        Return JSON with the following structure:
        {
            ""season"": ""string"",
            ""subSeason"": ""string"",
            ""description"": ""string"",
            ""recommendedColors"": [""#hexcode1"", ""#hexcode2""],
            ""bestMetals"": ""string""
        }";

        var text = await GenerateContentAsync(prompt, imageBase64);
        text = CleanJsonResponse(text);

        var result = JsonSerializer.Deserialize<SeasonAnalysisResult>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? new SeasonAnalysisResult();
    }

    public async Task<string> DescribeClothingAsync(string imageBase64)
    {
        return await DescribeImageAsync(imageBase64, 
            "Describe this clothing item in detail for a fashion visualizer. Include color, fabric texture, fit, neckline, and key details. Keep it concise.");
    }

    private async Task<string> DescribeImageAsync(string imageBase64, string prompt)
    {
        try
        {
            return await GenerateContentAsync(prompt, imageBase64);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Description generation failed: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<string> GenerateTryOnAsync(string personImageBase64, string clothingImageBase64)
    {
        // Describe both images to provide semantic context
        var personDescTask = DescribeImageAsync(personImageBase64, 
            "Describe the person in this photo (gender, body type, pose, hair) and the background. Concise.");
        var clothingDescTask = DescribeImageAsync(clothingImageBase64, 
            "Describe this clothing item (type, color, material, style). Concise.");

        await Task.WhenAll(personDescTask, clothingDescTask);

        var personDesc = await personDescTask;
        var clothingDesc = await clothingDescTask;

        // Construct Prompt for image generation
        var prompt = $@"A high-quality, photorealistic fashion shot of {personDesc} wearing {clothingDesc}. 
        Maintain the person's exact pose, facial features, and the original background from the reference image.
        The clothing should fit naturally. 8k resolution.";

        // Use Imagen for image generation
        var url = $"{BaseUrl}/models/imagen-3.0-generate-001:predict?key={_apiKey}";

        var request = new
        {
            instances = new[]
            {
                new
                {
                    prompt = prompt
                }
            },
            parameters = new
            {
                sampleCount = 1
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ImagenResponse>();
            var imageData = result?.Predictions?.FirstOrDefault()?.BytesBase64Encoded;
            
            if (!string.IsNullOrEmpty(imageData))
            {
                return $"data:image/jpeg;base64,{imageData}";
            }

            throw new InvalidOperationException("No image generated");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate try-on image: {ex.Message}", ex);
        }
    }

    public async Task<OutfitSuggestion> SuggestOutfitAsync(IEnumerable<WardrobeItem> wardrobeItems, string occasion, string weather)
    {
        var items = wardrobeItems.Select(i => new 
        { 
            id = i.Id, 
            type = i.Category.ToString().ToLower(), 
            tags = i.Tags, 
            color = i.Color 
        });
        
        var itemsJson = JsonSerializer.Serialize(items);

        var prompt = $@"Given these wardrobe items: {itemsJson}.
        Suggest one complete outfit for a {occasion} occasion where the weather is {weather}.
        Select specific Item IDs from the list. Explain why it works.
        Return JSON with the following structure:
        {{
            ""topId"": ""string"",
            ""bottomId"": ""string"",
            ""shoeId"": ""string"",
            ""reasoning"": ""string"",
            ""styleTip"": ""string""
        }}";

        var text = await GenerateContentAsync(prompt);
        text = CleanJsonResponse(text);

        var result = JsonSerializer.Deserialize<OutfitSuggestion>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? new OutfitSuggestion();
    }

    private static string CleanJsonResponse(string text)
    {
        // Remove markdown code blocks if present
        if (text.StartsWith("```json"))
        {
            text = text[7..];
        }
        else if (text.StartsWith("```"))
        {
            text = text[3..];
        }

        if (text.EndsWith("```"))
        {
            text = text[..^3];
        }

        return text.Trim();
    }
}

// Response models
internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<Candidate>? Candidates { get; set; }
}

internal class Candidate
{
    [JsonPropertyName("content")]
    public ContentResponse? Content { get; set; }
}

internal class ContentResponse
{
    [JsonPropertyName("parts")]
    public List<PartResponse>? Parts { get; set; }
}

internal class PartResponse
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal class ImagenResponse
{
    [JsonPropertyName("predictions")]
    public List<ImagenPrediction>? Predictions { get; set; }
}

internal class ImagenPrediction
{
    [JsonPropertyName("bytesBase64Encoded")]
    public string? BytesBase64Encoded { get; set; }
}
