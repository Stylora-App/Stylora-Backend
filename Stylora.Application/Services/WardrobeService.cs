using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class WardrobeService : IWardrobeService
{
    private readonly IWardrobeRepository _wardrobeRepository;
    private readonly IClothingValidationService _clothingValidationService;

    public WardrobeService(
        IWardrobeRepository wardrobeRepository,
        IClothingValidationService clothingValidationService)
    {
        _wardrobeRepository = wardrobeRepository;
        _clothingValidationService = clothingValidationService;
    }

    public async Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId)
    {
        var items = await _wardrobeRepository.GetAllItemsAsync(userId);
        return items.Select(MapToDto);
    }

    public async Task<WardrobeValidationDto> AnalyzeItemAsync(AnalyzeWardrobeItemRequest request)
    {
        var validation = await _clothingValidationService.ValidateAsync(request.Image);
        return MapValidation(validation);
    }

    public async Task<CreateWardrobeItemResponse> AddItemAsync(string userId, CreateWardrobeItemRequest request)
    {
        var validation = await _clothingValidationService.ValidateAsync(request.Image);
        var validationDto = MapValidation(validation);

        if (validation.Status == ClothingValidationStatus.Warning && !request.OverrideValidationWarning)
        {
            return new CreateWardrobeItemResponse
            {
                Validation = validationDto
            };
        }

        var resolvedCategory = string.IsNullOrWhiteSpace(request.Category)
            ? validation.SuggestedCategory
            : request.Category;

        if (!Enum.TryParse<ClothingCategory>(resolvedCategory, true, out var category))
            category = ClothingCategory.Top;

        var resolvedStyle = string.IsNullOrWhiteSpace(request.Style)
            ? validation.SuggestedStyle
            : request.Style;

        var resolvedColor = string.IsNullOrWhiteSpace(request.Color)
            ? validation.SuggestedColor
            : request.Color;

        var item = new WardrobeItem
        {
            ImagePath = request.Image,
            Category = category,
            ArticleTypeLabel = validation.SuggestedArticleType,
            AudienceTag = validation.SuggestedGender,
            Style = Enum.TryParse<StylePreference>(resolvedStyle, true, out var style) ? style : null,
            Color = await _wardrobeRepository.ResolveColorAsync(resolvedColor),
            ValidationStatus = validation.Status,
            ValidationConfidence = validation.Confidence,
            ValidationMessage = validation.Message,
            ValidatedAt = DateTime.UtcNow
        };

        var savedItem = await _wardrobeRepository.AddItemAsync(userId, item);
        return new CreateWardrobeItemResponse
        {
            Item = MapToDto(savedItem),
            Validation = validationDto
        };
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        return await _wardrobeRepository.DeleteItemAsync(userId, itemId);
    }

    public async Task IncrementWornCountAsync(string userId, string itemId)
    {
        await _wardrobeRepository.IncrementWornCountAsync(userId, itemId);
    }

    private static WardrobeItemDto MapToDto(WardrobeItem item)
    {
        return new WardrobeItemDto
        {
            Id = item.Id.ToString(),
            Image = item.ImagePath,
            Category = item.Category.ToString().ToLowerInvariant(),
            ArticleTypeLabel = item.ArticleTypeLabel,
            AudienceTag = item.AudienceTag,
            Style = item.Style?.ToString().ToLowerInvariant(),
            Color = item.Color?.HexCode ?? item.Color?.Name,
            WornCount = item.WornCount,
            ValidationStatus = item.ValidationStatus?.ToString().ToLowerInvariant(),
            ValidationConfidence = item.ValidationConfidence,
            ValidationMessage = item.ValidationMessage,
            ValidatedAt = item.ValidatedAt
        };
    }

    private static WardrobeValidationDto MapValidation(ClothingImageValidationResult validation)
    {
        return new WardrobeValidationDto
        {
            Status = validation.Status.ToString().ToLowerInvariant(),
            IsLikelyClothing = validation.IsLikelyClothing,
            Confidence = validation.Confidence,
            Message = validation.Message,
            CanOverride = validation.Status == ClothingValidationStatus.Warning,
            NearestLabels = validation.NearestLabels.ToList(),
            SuggestedCategory = validation.SuggestedCategory,
            SuggestedArticleType = validation.SuggestedArticleType,
            SuggestedStyle = validation.SuggestedStyle,
            SuggestedColor = validation.SuggestedColor,
            SuggestedColorFamily = validation.SuggestedColorFamily,
            SuggestedUsage = validation.SuggestedUsage,
            SuggestedGender = validation.SuggestedGender
        };
    }
}
