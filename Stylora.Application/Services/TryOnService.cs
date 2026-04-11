using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;

namespace Stylora.Application.Services;

public class TryOnService : ITryOnService
{
    private readonly IGeminiService _geminiService;

    public TryOnService(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<TryOnResponse> GenerateTryOnAsync(TryOnRequest request)
    {
        var generatedImage = await _geminiService.GenerateTryOnAsync(
            request.PersonImageBase64,
            request.ClothingImageBase64
        );

        return new TryOnResponse
        {
            GeneratedImage = generatedImage
        };
    }
}
