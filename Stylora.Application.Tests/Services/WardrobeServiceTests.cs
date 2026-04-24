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
            => Task.FromResult(false);

        public Task<IEnumerable<WardrobeItem>> GetAllItemsAsync(string userId)
            => Task.FromResult<IEnumerable<WardrobeItem>>(Items);

        public Task<WardrobeItem?> GetItemByIdAsync(string userId, string itemId)
            => Task.FromResult<WardrobeItem?>(Items.FirstOrDefault());

        public Task IncrementWornCountAsync(string userId, string itemId)
            => Task.CompletedTask;

        public Task<WardrobeItem?> UpdateItemAsync(string userId, WardrobeItem item)
            => Task.FromResult<WardrobeItem?>(item);
    }
}
