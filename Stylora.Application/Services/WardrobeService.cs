using Stylora.Application.DTOs;
using Stylora.Application.ClothingTags;
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

        var resolvedArticleType = string.IsNullOrWhiteSpace(request.ArticleTypeLabel)
            ? validation.SuggestedArticleType
            : ClothingTagTaxonomy.NormalizeArticleType(request.ArticleTypeLabel);

        var resolvedCategory = string.IsNullOrWhiteSpace(request.Category)
            ? ClothingTagTaxonomy.ResolveCategory(validation.SuggestedCategory, resolvedArticleType)
            : request.Category;

        if (!Enum.TryParse<ClothingCategory>(resolvedCategory, true, out var category))
            category = ClothingCategory.Top;

        var resolvedAudienceTag = string.IsNullOrWhiteSpace(request.AudienceTag)
            ? validation.SuggestedGender
            : ClothingTagTaxonomy.NormalizeAudienceTag(request.AudienceTag);

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
            ArticleTypeLabel = resolvedArticleType,
            AudienceTag = resolvedAudienceTag,
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

    public async Task<int> DeleteItemsAsync(string userId, IReadOnlyCollection<string> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return 0;
        }

        return await _wardrobeRepository.DeleteItemsAsync(userId, itemIds);
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
            OutfitRole = ClothingTagTaxonomy.ResolveOutfitRole(item.Category.ToString(), item.ArticleTypeLabel),
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
            SuggestedGender = validation.SuggestedGender,
            SuggestedOutfitRole = validation.SuggestedOutfitRole
        };
    }
}
