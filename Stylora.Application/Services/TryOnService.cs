using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Domain.Entities;

namespace Stylora.Application.Services;

public class TryOnService : ITryOnService
{
    private readonly IGeminiService _geminiService;
    private readonly ITryOnRepository _tryOnRepository;
    private readonly HttpClient _httpClient;

    public TryOnService(IGeminiService geminiService, ITryOnRepository tryOnRepository)
    {
        _geminiService = geminiService;
        _tryOnRepository = tryOnRepository;
        _httpClient = new HttpClient();
    }

    public async Task<TryOnResponse> GenerateTryOnAsync(TryOnRequest request, Guid userId)
    {
        var clothingBase64 = request.ClothingImageBase64;
        if (string.IsNullOrWhiteSpace(clothingBase64) && !string.IsNullOrWhiteSpace(request.ClothingImageUrl))
        {
            var bytes = await _httpClient.GetByteArrayAsync(request.ClothingImageUrl);
            clothingBase64 = Convert.ToBase64String(bytes);
        }

        var session = new TryOnSession
        {
            UserId = userId,
            PersonImagePath = request.PersonImageBase64,
            ClothingImagePath = clothingBase64,
        };
        session = await _tryOnRepository.CreateAsync(session);

        try
        {
            var generatedImage = await _geminiService.GenerateTryOnAsync(
                request.PersonImageBase64,
                clothingBase64
            );

            session.GeneratedImagePath = generatedImage;
            session.IsSuccessful = true;
            await _tryOnRepository.UpdateAsync(session);

            return new TryOnResponse { GeneratedImage = generatedImage };
        }
        catch (Exception ex)
        {
            session.IsSuccessful = false;
            session.ErrorMessage = ex.Message;
            await _tryOnRepository.UpdateAsync(session);
            throw;
        }
    }

    public async Task<LastTryOnPhotoDto?> GetLastPersonPhotoAsync(Guid userId)
    {
        var sessions = await _tryOnRepository.GetByUserIdAsync(userId);
        var latest = sessions.FirstOrDefault();
        if (latest == null) return null;

        return new LastTryOnPhotoDto { PersonImageBase64 = latest.PersonImagePath };
    }
}
