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
    [Fact]
    public async Task AddItemAsync_SavesImmediately_WhenValidationPasses()
    {
        var repository = new FakeWardrobeRepository();
        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Pass,
                IsLikelyClothing = true,
                Confidence = 0.91,
                Message = "ok",
                NearestLabels = ["clothing"]
            }));

        var response = await service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "top"
        });

        Assert.NotNull(response.Item);
        Assert.NotNull(response.Validation);
        Assert.Single(repository.Items);
        Assert.Equal("pass", response.Item!.ValidationStatus);
    }

    [Fact]
    public async Task AddItemAsync_ReturnsWarningWithoutSaving_WhenOverrideIsFalse()
    {
        var repository = new FakeWardrobeRepository();
        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Warning,
                IsLikelyClothing = false,
                Confidence = 0.87,
                Message = "warning",
                NearestLabels = ["non_clothing"]
            }));

        var response = await service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "top",
            OverrideValidationWarning = false
        });

        Assert.Null(response.Item);
        Assert.NotNull(response.Validation);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task AddItemAsync_SavesWarningResult_WhenOverrideIsTrue()
    {
        var repository = new FakeWardrobeRepository();
        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Warning,
                IsLikelyClothing = true,
                Confidence = 0.58,
                Message = "warning",
                NearestLabels = ["clothing", "non_clothing"]
            }));

        var response = await service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "top",
            OverrideValidationWarning = true
        });

        Assert.NotNull(response.Item);
        Assert.Equal(ClothingValidationStatus.Warning, repository.Items.Single().ValidationStatus);
        Assert.Equal("warning", response.Validation!.Status);
    }

    [Fact]
    public async Task AddItemAsync_UsesSuggestedMetadata_WhenRequestOmitsManualFields()
    {
        var repository = new FakeWardrobeRepository();
        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Pass,
                IsLikelyClothing = true,
                Confidence = 0.97,
                Message = "ok",
                NearestLabels = ["clothing"],
                SuggestedCategory = "outerwear",
                SuggestedStyle = "casual",
                SuggestedColor = "navy blue",
                SuggestedGender = "women",
                SuggestedArticleType = "jacket",
                SuggestedOutfitRole = "layer"
            }));

        var response = await service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8="
        });

        var saved = Assert.Single(repository.Items);
        Assert.Equal(ClothingCategory.Outerwear, saved.Category);
        Assert.Equal(StylePreference.Casual, saved.Style);
        Assert.Equal("navy blue", saved.Color?.Name);
        Assert.Equal("women", saved.AudienceTag);
        Assert.Equal("jacket", saved.ArticleTypeLabel);
        Assert.Equal("outerwear", response.Item!.Category);
        Assert.Equal("navy blue", response.Item.Color);
        Assert.Equal("layer", response.Item.OutfitRole);
    }

    [Fact]
    public async Task AddItemAsync_UsesManualOverrideTags_WhenRequestProvidesThem()
    {
        var repository = new FakeWardrobeRepository();
        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Pass,
                IsLikelyClothing = true,
                Confidence = 0.82,
                Message = "ok",
                NearestLabels = ["clothing"],
                SuggestedCategory = "top",
                SuggestedArticleType = "shirt",
                SuggestedGender = "unisex",
                SuggestedColor = "red"
            }));

        var response = await service.AddItemAsync(Guid.NewGuid().ToString(), new CreateWardrobeItemRequest
        {
            Image = "data:image/png;base64,aGVsbG8=",
            Category = "outerwear",
            ArticleTypeLabel = "blazer",
            AudienceTag = "men",
            Style = "Formal",
            Color = "charcoal"
        });

        var saved = Assert.Single(repository.Items);
        Assert.Equal("blazer", saved.ArticleTypeLabel);
        Assert.Equal("men", saved.AudienceTag);
        Assert.Equal(StylePreference.Formal, saved.Style);
        Assert.Equal("charcoal", saved.Color?.Name);
        Assert.Equal("layer", response.Item!.OutfitRole);
    }

    [Fact]
    public async Task DeleteItemsAsync_DeletesOnlyRequestedItems()
    {
        var repository = new FakeWardrobeRepository();
        var first = new WardrobeItem { Id = Guid.NewGuid() };
        var second = new WardrobeItem { Id = Guid.NewGuid() };
        var third = new WardrobeItem { Id = Guid.NewGuid() };
        repository.Items.AddRange([first, second, third]);

        var service = new WardrobeService(
            repository,
            new FakeClothingValidationService(new ClothingImageValidationResult()));

        var deleted = await service.DeleteItemsAsync(Guid.NewGuid().ToString(), [first.Id.ToString(), third.Id.ToString()]);

        Assert.Equal(2, deleted);
        Assert.Single(repository.Items);
        Assert.Equal(second.Id, repository.Items.Single().Id);
    }

    private sealed class FakeClothingValidationService : IClothingValidationService
    {
        private readonly ClothingImageValidationResult _result;

        public FakeClothingValidationService(ClothingImageValidationResult result)
        {
            _result = result;
        }

        public Task<ClothingImageValidationResult> ValidateAsync(string imageBase64, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeWardrobeRepository : IWardrobeRepository
    {
        public List<WardrobeItem> Items { get; } = [];

        public Task<WardrobeItem> AddItemAsync(string userId, WardrobeItem item)
        {
            item.Id = Guid.NewGuid();
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task<bool> DeleteItemAsync(string userId, string itemId)
        {
            var removed = Items.RemoveAll(item => item.Id.ToString() == itemId);
            return Task.FromResult(removed > 0);
        }

        public Task<int> DeleteItemsAsync(string userId, IEnumerable<string> itemIds)
        {
            var ids = itemIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed = Items.RemoveAll(item => ids.Contains(item.Id.ToString()));
            return Task.FromResult(removed);
        }

        public Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string userId)
            => Task.FromResult<IEnumerable<WardrobeItem>>(Items);

        public Task<WardrobeItem?> GetItemByIdAsync(string userId, string itemId)
            => Task.FromResult<WardrobeItem?>(Items.FirstOrDefault());

        public Task<Color?> ResolveColorAsync(string? colorName)
            => Task.FromResult<Color?>(string.IsNullOrWhiteSpace(colorName) ? null : new Color
            {
                Id = Guid.NewGuid(),
                Name = colorName.Trim().ToLowerInvariant()
            });

        public Task<WardrobeItem?> UpdateItemAsync(string userId, WardrobeItem item)
            => Task.FromResult<WardrobeItem?>(item);
    }
}
