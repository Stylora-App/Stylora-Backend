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
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Gemini API error ({response.StatusCode}): {errorBody}. " +
                "Verify your GEMINI_API_KEY is valid and the Generative Language API is enabled.");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "";
    }

    public async Task<SeasonAnalysisResult> AnalyzeSeasonAsync(string imageBase64)
    {
        var prompt = @"Analyze this person's skin undertone, eye color, and hair color strictly according to Armochromia theory. 
        Determine their seasonal color palette (Spring, Summer, Autumn, Winter) and specific sub-season (e.g., Deep Autumn, Light Summer).
        Provide a list of 8-12 recommended colors as hex codes (e.g., #FF5733) and a brief explanation.
        Return JSON with the following structure:
        {
            ""season"": ""string"",
            ""subSeason"": ""string"",
            ""description"": ""string"",
            ""recommendedColors"": [""#hexcode1"", ""#hexcode2"", ""#hexcode3"", ...],
            ""bestMetals"": ""string""
        }";

        var text = await GenerateContentAsync(prompt, imageBase64);
        text = CleanJsonResponse(text);

        var rawResult = JsonSerializer.Deserialize<SeasonAnalysisRaw>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Convert to domain entity with hex codes
        var result = new SeasonAnalysisResult
        {
            Season = rawResult?.Season ?? "",
            SubSeason = rawResult?.SubSeason ?? "",
            Description = rawResult?.Description ?? "",
            BestMetals = rawResult?.BestMetals ?? "",
            RecommendedColors = rawResult?.RecommendedColors?.Select(hexCode => new RecommendedColor
            {
                Color = new Color { Name = hexCode, HexCode = hexCode }
            }).ToList() ?? []
        };

        return result;
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
        var url = $"{BaseUrl}/models/gemini-2.5-flash-image:generateContent?key={_apiKey}";

        var prompt = @"Take the person from the first image and dress them in the garment from the second image. 
Generate a photorealistic image of the exact same person wearing that exact garment. 
Keep the person's face, body, pose, hair, and background completely unchanged. 
Only replace their clothing with the garment from the second image. 
The garment should fit naturally on the person's body.";

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "image/jpeg",
                                data = personImageBase64
                            }
                        },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "image/jpeg",
                                data = clothingImageBase64
                            }
                        },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE", "TEXT" }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Gemini API error ({response.StatusCode}): {errorContent}. " +
                    "Verify your GEMINI_API_KEY is valid and the Generative Language API is enabled.");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiImageResponse>();
            var parts = result?.Candidates?.FirstOrDefault()?.Content?.Parts;

            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part.InlineData != null && !string.IsNullOrEmpty(part.InlineData.Data))
                    {
                        var mimeType = part.InlineData.MimeType ?? "image/png";
                        return $"data:{mimeType};base64,{part.InlineData.Data}";
                    }
                }
            }

            throw new InvalidOperationException("No image generated in response");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to generate try-on image: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Unexpected error during try-on generation: {ex.Message}", ex);
        }
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

// Response models for gemini-2.5-flash-image
internal class GeminiImageResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiImageCandidate>? Candidates { get; set; }
}

internal class GeminiImageCandidate
{
    [JsonPropertyName("content")]
    public GeminiImageContent? Content { get; set; }
}

internal class GeminiImageContent
{
    [JsonPropertyName("parts")]
    public List<GeminiImagePart>? Parts { get; set; }
}

internal class GeminiImagePart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    public GeminiInlineData? InlineData { get; set; }
}

internal class GeminiInlineData
{
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

// Raw response models for parsing Gemini API responses
internal class SeasonAnalysisRaw
{
    public string? Season { get; set; }
    public string? SubSeason { get; set; }
    public string? Description { get; set; }
    public List<string>? RecommendedColors { get; set; }
    public string? BestMetals { get; set; }
}
