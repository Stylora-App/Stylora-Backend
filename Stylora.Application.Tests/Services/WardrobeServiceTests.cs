using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class WardrobeServiceTests
{
    private readonly Mock<IWardrobeRepository> _repository = new();
    private readonly Mock<IClothingValidationService> _validator = new();
    private readonly WardrobeService _service;
    private WardrobeItem? _savedItem;

    public WardrobeServiceTests()
    {
        _repository
            .Setup(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()))
            .Callback<string, WardrobeItem>((_, item) =>
            {
                item.Id = Guid.NewGuid();
                _savedItem = item;
            })
            .ReturnsAsync((string _, WardrobeItem item) => item);
        _repository
            .Setup(r => r.ResolveColorAsync(It.IsAny<string?>()))
            .ReturnsAsync((string? name) => string.IsNullOrWhiteSpace(name)
                ? null
                : new Color { Id = Guid.NewGuid(), Name = name.Trim().ToLowerInvariant() });
        _service = new WardrobeService(_repository.Object, _validator.Object);
    }

    [Fact]
    public async Task AddItemAsync_ValidationPasses_SavesItemImmediately()
    {
        // Arrange
        SetupValidation(PassResult());

        // Act
        var response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "top"
        });

        // Assert
        Assert.NotNull(response.Item);
        Assert.Equal("pass", response.Item!.ValidationStatus);
        _repository.Verify(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_WarningWithoutOverride_ReturnsValidationWithoutSaving()
    {
        // Arrange
        SetupValidation(WarningResult());

        // Act
        var response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            OverrideValidationWarning = false
        });

        // Assert
        Assert.Null(response.Item);
        Assert.NotNull(response.Validation);
        Assert.True(response.Validation!.CanOverride);
        _repository.Verify(r => r.AddItemAsync(It.IsAny<string>(), It.IsAny<WardrobeItem>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_WarningWithOverride_SavesItemWithWarningStatus()
    {
        // Arrange
        SetupValidation(WarningResult());

        // Act
        var response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            OverrideValidationWarning = true
        });

        // Assert
        Assert.NotNull(response.Item);
        Assert.Equal(ClothingValidationStatus.Warning, _savedItem!.ValidationStatus);
        Assert.Equal("warning", response.Validation!.Status);
    }

    [Fact]
    public async Task AddItemAsync_RequestOmitsManualFields_UsesSuggestedMetadata()
    {
        // Arrange
        SetupValidation(PassResult(
            category: "outerwear", articleType: "jacket", style: "casual",
            color: "navy blue", gender: "women", outfitRole: "layer"));

        // Act
        var response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8="
        });

        // Assert
        Assert.Equal(ClothingCategory.Outerwear, _savedItem!.Category);
        Assert.Equal(StylePreference.Casual, _savedItem.Style);
        Assert.Equal("navy blue", _savedItem.Color?.Name);
        Assert.Equal("women", _savedItem.AudienceTag);
        Assert.Equal("jacket", _savedItem.ArticleTypeLabel);
        Assert.Equal("layer", response.Item!.OutfitRole);
    }

    [Fact]
    public async Task AddItemAsync_RequestProvidesManualTags_OverridesSuggestions()
    {
        // Arrange
        SetupValidation(PassResult(category: "top", articleType: "shirt", gender: "unisex", color: "red"));

        // Act
        var response = await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "outerwear",
            ArticleTypeLabel = "blazer",
            AudienceTag = "men",
            Style = "Formal",
            Color = "charcoal"
        });

        // Assert
        Assert.Equal("blazer", _savedItem!.ArticleTypeLabel);
        Assert.Equal("men", _savedItem.AudienceTag);
        Assert.Equal(StylePreference.Formal, _savedItem.Style);
        Assert.Equal("charcoal", _savedItem.Color?.Name);
        Assert.Equal("layer", response.Item!.OutfitRole);
    }

    [Theory]
    [InlineData("outerwear", ClothingCategory.Outerwear)]
    [InlineData("DRESS", ClothingCategory.Dress)]
    [InlineData("nonsense", ClothingCategory.Top)]
    public async Task AddItemAsync_CategoryInputVariants_ResolvesCategoryWithTopFallback(string category, ClothingCategory expected)
    {
        // Arrange
        SetupValidation(PassResult());

        // Act
        await _service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = category
        });

        // Assert
        Assert.Equal(expected, _savedItem!.Category);
    }

    [Fact]
    public async Task AnalyzeItemAsync_WarningResult_MapsValidationWithOverrideFlag()
    {
        // Arrange
        SetupValidation(WarningResult());

        // Act
        var validation = await _service.AnalyzeItemAsync(new AnalyzeWardrobeItemRequest { Image = "data:image/png;base64,aGVsbG8=" });

        // Assert
        Assert.Equal("warning", validation.Status);
        Assert.True(validation.CanOverride);
        _validator.Verify(v => v.ValidateAsync("data:image/png;base64,aGVsbG8=", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllItemsAsync_ItemsExist_MapsEntitiesToDtos()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _repository
            .Setup(r => r.GetAllItemsAsync(userId))
            .ReturnsAsync(
            [
                new WardrobeItem
                {
                    Id = Guid.NewGuid(),
                    Category = ClothingCategory.Shoes,
                    Style = StylePreference.Sport,
                    Color = new Color { Name = "white", HexCode = "#FFFFFF" }
                }
            ]);

        // Act
        var items = (await _service.GetAllItemsAsync(userId)).ToList();

        // Assert
        var dto = Assert.Single(items);
        Assert.Equal("shoes", dto.Category);
        Assert.Equal("sport", dto.Style);
        Assert.Equal("#FFFFFF", dto.Color);
    }

    [Fact]
    public async Task DeleteItemsAsync_EmptyIdList_ReturnsZeroWithoutCallingRepository()
    {
        // Act
        var deleted = await _service.DeleteItemsAsync(Guid.NewGuid().ToString(), []);

        // Assert
        Assert.Equal(0, deleted);
        _repository.Verify(r => r.DeleteItemsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItemsAsync_IdsProvided_DelegatesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        _repository
            .Setup(r => r.DeleteItemsAsync(userId, ids))
            .ReturnsAsync(2);

        // Act
        var deleted = await _service.DeleteItemsAsync(userId, ids);

        // Assert
        Assert.Equal(2, deleted);
        _repository.Verify(r => r.DeleteItemsAsync(userId, ids), Times.Once);
    }

    private void SetupValidation(ClothingImageValidationResult result)
        => _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static ClothingImageValidationResult PassResult(
        string? category = null,
        string? articleType = null,
        string? style = null,
        string? color = null,
        string? gender = null,
        string? outfitRole = null) => new()
    {
        Status = ClothingValidationStatus.Pass,
        IsLikelyClothing = true,
        Confidence = 0.93,
        Message = "ok",
        NearestLabels = ["clothing"],
        SuggestedCategory = category,
        SuggestedArticleType = articleType,
        SuggestedStyle = style,
        SuggestedColor = color,
        SuggestedGender = gender,
        SuggestedOutfitRole = outfitRole
    };

    private static ClothingImageValidationResult WarningResult() => new()
    {
        Status = ClothingValidationStatus.Warning,
        IsLikelyClothing = false,
        Confidence = 0.81,
        Message = "warning",
        NearestLabels = ["non_clothing"]
    };
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
