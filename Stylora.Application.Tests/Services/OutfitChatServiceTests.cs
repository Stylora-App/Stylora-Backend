using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class OutfitChatServiceTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsOutOfScope_WhenConversationIsNotAboutOutfits()
    {
        var service = CreateService();

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Tell me a joke." }
            ]
        });

        Assert.Equal("out_of_scope", response.Status);
        Assert.Null(response.Outfit);
    }

    [Fact]
    public async Task ProcessAsync_AsksForWeather_WhenOccasionExistsButWeatherDoesNot()
    {
        var service = CreateService();

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Build me a work outfit." }
            ]
        });

        Assert.Equal("follow_up", response.Status);
        Assert.Contains("weather", response.MissingFields);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsOutfit_WhenConversationHasEnoughContextAndPieces()
    {
        var wardrobeService = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "1", Category = "top", ArticleTypeLabel = "shirt", Style = "office", AudienceTag = "women", Image = "top.png", Color = "white", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "2", Category = "bottom", ArticleTypeLabel = "trousers", Style = "formal", AudienceTag = "women", Image = "bottom.png", Color = "black", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "3", Category = "shoes", ArticleTypeLabel = "heels", Style = "elegant", AudienceTag = "women", Image = "shoes.png", Color = "black", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "4", Category = "outerwear", ArticleTypeLabel = "blazer", Style = "office", AudienceTag = "women", Image = "layer.png", Color = "black", ValidationStatus = "pass" }
        ]);
        var userService = new FakeUserService(new UserProfileDto
        {
            Season = "Autumn",
            Palette = ["#6B7A5E", "#CBB89D"]
        });
        var service = new OutfitChatService(wardrobeService, userService);

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Create a work outfit for a rainy and cool day." }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.True(response.Outfit!.Items.Count >= 3);
    }

    private static OutfitChatService CreateService()
    {
        return new OutfitChatService(
            new FakeWardrobeService([]),
            new FakeUserService(new UserProfileDto()));
    }

    private sealed class FakeWardrobeService : IWardrobeService
    {
        private readonly IReadOnlyCollection<WardrobeItemDto> _items;

        public FakeWardrobeService(IReadOnlyCollection<WardrobeItemDto> items)
        {
            _items = items;
        }

        public Task<IEnumerable<WardrobeItemDto>> GetAllItemsAsync(string userId)
            => Task.FromResult<IEnumerable<WardrobeItemDto>>(_items);

        public Task<WardrobeValidationDto> AnalyzeItemAsync(AnalyzeWardrobeItemRequest request)
            => throw new NotSupportedException();

        public Task<CreateWardrobeItemResponse> AddItemAsync(string userId, CreateWardrobeItemRequest request)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(string userId, string itemId)
            => throw new NotSupportedException();

        public Task<int> DeleteItemsAsync(string userId, IReadOnlyCollection<string> itemIds)
            => throw new NotSupportedException();
    }

    private sealed class FakeUserService : IUserService
    {
        private readonly UserProfileDto _profile;

        public FakeUserService(UserProfileDto profile)
        {
            _profile = profile;
        }

        public Task<UserProfileDto> GetProfileAsync(string userId)
            => Task.FromResult(_profile);

        public Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request)
            => throw new NotSupportedException();

        public UserDto MapToUserDto(User user)
            => throw new NotSupportedException();

        public UserProfileDto MapToProfileDto(User user)
            => throw new NotSupportedException();
    }
}
