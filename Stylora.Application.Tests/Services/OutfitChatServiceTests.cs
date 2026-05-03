using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class OutfitChatServiceTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsOutOfScope_WhenConversationIsNotAboutOutfits()
    {
        var service = CreateService(
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "out_of_scope",
                IsInScope = false
            }),
            new FakeWeatherService(null));

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
        var service = CreateService(
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = true,
                OccasionText = "work",
                StyleBucket = "office"
            }),
            new FakeWeatherService(null));

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
        var service = new OutfitChatService(
            wardrobeService,
            userService,
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = true,
                OccasionText = "work",
                StyleBucket = "office",
                WeatherSummary = "rainy, cool",
                WeatherStatus = "rainy",
                TemperatureC = 11
            }),
            new FakeWeatherService(new ResolvedWeatherContext
            {
                Status = "rainy",
                TemperatureC = 11,
                ThermalBand = "cold",
                Summary = "rainy, cold, 11C"
            }));

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

    [Fact]
    public async Task ProcessAsync_ChangesLeadPiece_WhenPaletteChanges()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "green-top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "green.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "pink-top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "pink.png", Color = "pink", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "jeans", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "jeans.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "sneakers", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var parser = new FakeIntentParser(new OutfitIntentResult
        {
            Intent = "generate_outfit",
            IsInScope = true,
            OccasionText = "weekend",
            StyleBucket = "casual",
            WeatherStatus = "sunny",
            TemperatureC = 22
        });
        var weather = new FakeWeatherService(new ResolvedWeatherContext
        {
            Status = "sunny",
            TemperatureC = 22,
            ThermalBand = "warm",
            Summary = "sunny, warm, 22C"
        });

        var greenService = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E"] }),
            parser,
            weather);
        var pinkService = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#F0A0B5"] }),
            parser,
            weather);

        var request = new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Build me a sunny weekend outfit." }
            ]
        };

        var greenResponse = await greenService.ProcessAsync(Guid.NewGuid().ToString(), request);
        var pinkResponse = await pinkService.ProcessAsync(Guid.NewGuid().ToString(), request);

        Assert.Equal("green-top", greenResponse.Outfit!.Items.First().Id);
        Assert.Equal("pink-top", pinkResponse.Outfit!.Items.First().Id);
    }

    [Fact]
    public async Task ProcessAsync_UsesStandaloneLocationReply_FromFollowUpConversation()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "top.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "bottom", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "bottom.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "shoes", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var service = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E"] }),
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = false,
                OccasionText = "weekend",
                StyleBucket = "casual"
            }),
            new ConditionalWeatherService());

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "assistant", Content = "Describe the occasion and weather, and I will build an outfit only from your Stylora wardrobe." },
                new OutfitChatMessageDto { Role = "user", Content = "Build me a weekend outfit." },
                new OutfitChatMessageDto { Role = "assistant", Content = "What weather should I dress for? You can describe it directly or tell me the city." },
                new OutfitChatMessageDto { Role = "user", Content = "Brasov" }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.Contains("cloudy", response.Outfit!.WeatherSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_UsesCombinedStyleAndLocationReply_FromFollowUpConversation()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "top.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "bottom", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "bottom.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "shoes", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var service = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E"] }),
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = false
            }),
            new ConditionalWeatherService());

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "assistant", Content = "Describe the occasion and weather, and I will build an outfit only from your Stylora wardrobe." },
                new OutfitChatMessageDto { Role = "user", Content = "i need an outfit to go out for a drink tonight in Brasov" },
                new OutfitChatMessageDto { Role = "assistant", Content = "Tell me the occasion or vibe and either the weather or the city I should check." },
                new OutfitChatMessageDto { Role = "user", Content = "casual, Brasov" },
                new OutfitChatMessageDto { Role = "assistant", Content = "Tell me if this is for today or tomorrow, or describe the weather directly." },
                new OutfitChatMessageDto { Role = "user", Content = "tonight" }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.Equal("casual", response.Outfit!.Style, ignoreCase: true);
        Assert.Contains("cloudy", response.Outfit.WeatherSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_UsesStandaloneLocationAndDate_FromDirectRequest()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "top.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "bottom", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "bottom.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "shoes", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var service = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E"] }),
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = true,
                OccasionText = "weekend",
                StyleBucket = "casual"
            }),
            new ConditionalWeatherService());

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Build me a weekend outfit for Brasov tomorrow." }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.Contains("cloudy", response.Outfit!.WeatherSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_UsesNaturalSentenceLocationAndTonight_FromDirectRequest()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "top.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "bottom", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "bottom.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "shoes", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var service = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E"] }),
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = false
            }),
            new ConditionalWeatherService());

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "i need an outfit to go out tonight in Brasov for a casual drink" }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.Equal("casual", response.Outfit!.Style, ignoreCase: true);
        Assert.Contains("cloudy", response.Outfit.WeatherSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_SelectsNextCandidate_WhenShuffleMessageExistsEvenIfParserDoesNotCountIt()
    {
        var wardrobe = new FakeWardrobeService(
        [
            new WardrobeItemDto { Id = "green-top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "green.png", Color = "green", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "pink-top", Category = "top", ArticleTypeLabel = "shirt", Style = "casual", AudienceTag = "women", Image = "pink.png", Color = "pink", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "jeans", Category = "bottom", ArticleTypeLabel = "jeans", Style = "casual", AudienceTag = "women", Image = "jeans.png", Color = "blue", ValidationStatus = "pass" },
            new WardrobeItemDto { Id = "sneakers", Category = "shoes", ArticleTypeLabel = "sneakers", Style = "casual", AudienceTag = "women", Image = "shoes.png", Color = "white", ValidationStatus = "pass" }
        ]);

        var service = new OutfitChatService(
            wardrobe,
            new FakeUserService(new UserProfileDto { Palette = ["#6B7A5E", "#F0A0B5"] }),
            new FakeIntentParser(new OutfitIntentResult
            {
                Intent = "generate_outfit",
                IsInScope = true,
                OccasionText = "weekend",
                StyleBucket = "casual",
                Location = "Brasov",
                DateContext = "today",
                ShuffleCount = 0
            }),
            new ConditionalWeatherService());

        var response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest
        {
            Messages =
            [
                new OutfitChatMessageDto { Role = "user", Content = "Build me a casual outfit for Brasov today." },
                new OutfitChatMessageDto { Role = "assistant", Content = "I put together a casual look for weekend in cloudy, cool, 17C weather." },
                new OutfitChatMessageDto { Role = "user", Content = "Shuffle another option" }
            ]
        });

        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.StartsWith("Here is another", response.AssistantMessage, StringComparison.Ordinal);
    }

    private static OutfitChatService CreateService(IOutfitIntentParser intentParser, IWeatherService weatherService)
    {
        return new OutfitChatService(
            new FakeWardrobeService([]),
            new FakeUserService(new UserProfileDto()),
            intentParser,
            weatherService);
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

    private sealed class FakeIntentParser : IOutfitIntentParser
    {
        private readonly OutfitIntentResult _result;

        public FakeIntentParser(OutfitIntentResult result)
        {
            _result = result;
        }

        public Task<OutfitIntentResult> ParseAsync(IReadOnlyList<OutfitChatMessageDto> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeWeatherService : IWeatherService
    {
        private readonly ResolvedWeatherContext? _result;

        public FakeWeatherService(ResolvedWeatherContext? result)
        {
            _result = result;
        }

        public Task<ResolvedWeatherContext?> ResolveAsync(OutfitIntentResult intent, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class ConditionalWeatherService : IWeatherService
    {
        public Task<ResolvedWeatherContext?> ResolveAsync(OutfitIntentResult intent, CancellationToken cancellationToken = default)
        {
            if (string.Equals(intent.Location, "Brasov", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ResolvedWeatherContext?>(new ResolvedWeatherContext
                {
                    Source = "test",
                    LocationLabel = "Brasov",
                    Status = "cloudy",
                    TemperatureC = 17,
                    ThermalBand = "cool",
                    Summary = "cloudy, cool, 17C"
                });
            }

            return Task.FromResult<ResolvedWeatherContext?>(null);
        }
    }
}
