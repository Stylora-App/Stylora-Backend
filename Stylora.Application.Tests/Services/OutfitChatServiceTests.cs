using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class OutfitChatServiceTests
{
    private readonly Mock<IWardrobeService> _wardrobeService = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IOutfitIntentParser> _intentParser = new();
    private readonly Mock<IWeatherService> _weatherService = new();
    private readonly OutfitChatService _service;

    public OutfitChatServiceTests()
    {
        SetupWardrobe([]);
        SetupProfile(new UserProfileDto());
        SetupIntent(new OutfitIntentResult { Intent = "generate_outfit", IsInScope = true });
        SetupWeather(null);
        _service = new OutfitChatService(_wardrobeService.Object, _userService.Object, _intentParser.Object, _weatherService.Object);
    }

    [Fact]
    public async Task ProcessAsync_NoUserMessages_ReturnsFollowUpWithoutParsingIntent()
    {
        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest());

        // Assert
        Assert.Equal("follow_up", response.Status);
        Assert.Equal(["occasion", "weather"], response.MissingFields);
        _intentParser.Verify(
            p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ConversationNotAboutOutfits_ReturnsOutOfScope()
    {
        // Arrange
        SetupIntent(new OutfitIntentResult { Intent = "out_of_scope", IsInScope = false });

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(Msg("user", "Tell me a joke.")));

        // Assert
        Assert.Equal("out_of_scope", response.Status);
        Assert.Null(response.Outfit);
    }

    [Fact]
    public async Task ProcessAsync_UnrelatedFollowUpReply_ReturnsOutOfScope()
    {
        // Arrange
        SetupWardrobe(CasualWomenWardrobe());
        SetupProfile(new UserProfileDto { Palette = ["#6B7A5E"] });

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(
            Msg("assistant", "Tell me where you are headed, and I will put together a look from your saved wardrobe."),
            Msg("user", "pancakes")));

        // Assert
        Assert.Equal("out_of_scope", response.Status);
        Assert.Null(response.Outfit);
    }

    [Fact]
    public async Task ProcessAsync_OccasionKnownButWeatherUnresolved_AsksForWeather()
    {
        // Arrange
        SetupIntent(new OutfitIntentResult
        {
            Intent = "generate_outfit",
            IsInScope = true,
            OccasionText = "work",
            StyleBucket = "office"
        });

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(Msg("user", "Build me a work outfit.")));

        // Assert
        Assert.Equal("follow_up", response.Status);
        Assert.Contains("weather", response.MissingFields);
    }

    [Fact]
    public async Task ProcessAsync_EmptyWardrobe_ReturnsNotEnoughPieces()
    {
        // Arrange
        SetupIntent(CompleteIntent());
        SetupWeather(MildWeather());

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(Msg("user", "Build me a casual weekend outfit.")));

        // Assert
        Assert.Equal("not_enough_pieces", response.Status);
        Assert.Equal(["top", "bottom", "shoes"], response.MissingRoles);
    }

    [Theory]
    [InlineData("top", "bottom,shoes")]
    [InlineData("top,bottom", "shoes")]
    [InlineData("shoes", "top,bottom")]
    public async Task ProcessAsync_IncompleteWardrobe_ReportsMissingRoles(string ownedCategories, string expectedMissing)
    {
        // Arrange
        SetupIntent(CompleteIntent());
        SetupWeather(MildWeather());
        SetupWardrobe(ownedCategories.Split(',').Select(category => Item(category, category, null, audience: "men")).ToList());

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(Msg("user", "Build me a casual weekend outfit.")));

        // Assert
        Assert.Equal("not_enough_pieces", response.Status);
        Assert.Equal(expectedMissing.Split(','), response.MissingRoles);
    }

    [Fact]
    public async Task ProcessAsync_FullContextAndCompleteWardrobe_ReturnsOutfit()
    {
        // Arrange
        SetupWardrobe(OfficeWomenWardrobe());
        SetupProfile(new UserProfileDto { Season = "Autumn", Palette = ["#6B7A5E", "#CBB89D"] });
        SetupIntent(new OutfitIntentResult
        {
            Intent = "generate_outfit",
            IsInScope = true,
            OccasionText = "work",
            StyleBucket = "office",
            WeatherSummary = "rainy, cool",
            WeatherStatus = "rainy",
            TemperatureC = 11
        });
        SetupWeather(new ResolvedWeatherContext { Status = "rainy", TemperatureC = 11, ThermalBand = "cold", Summary = "rainy, cold, 11C" });

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(
            Msg("user", "Create a work outfit for a rainy and cool day.")));

        // Assert
        Assert.Equal("success", response.Status);
        Assert.NotNull(response.Outfit);
        Assert.True(response.Outfit!.Items.Count >= 3);
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulFlow_QueriesEachDependencyExactlyOnce()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        SetupWardrobe(CasualWomenWardrobe());
        SetupProfile(new UserProfileDto { Palette = ["#6B7A5E"] });
        SetupIntent(CompleteIntent());
        SetupWeather(MildWeather());

        // Act
        await _service.ProcessAsync(userId, Request(Msg("user", "Build me a casual weekend outfit.")));

        // Assert
        _intentParser.Verify(p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()), Times.Once);
        _weatherService.Verify(w => w.ResolveAsync(It.IsAny<OutfitIntentResult>(), It.IsAny<CancellationToken>()), Times.Once);
        _wardrobeService.Verify(w => w.GetAllItemsAsync(userId), Times.Once);
        _userService.Verify(u => u.GetProfileAsync(userId), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaletteChanges_ChangesLeadPiece()
    {
        // Arrange
        var wardrobe = new List<WardrobeItemDto>
        {
            Item("green-top", "top", "shirt", color: "green"),
            Item("pink-top", "top", "shirt", color: "pink"),
            Item("jeans", "bottom", "jeans", color: "blue"),
            Item("sneakers", "shoes", "sneakers", color: "white")
        };
        var request = Request(Msg("user", "Build me a sunny weekend outfit."));
        var greenService = CreateServiceWithPalette(wardrobe, "#6B7A5E");
        var pinkService = CreateServiceWithPalette(wardrobe, "#F0A0B5");

        // Act
        var greenResponse = await greenService.ProcessAsync(Guid.NewGuid().ToString(), request);
        var pinkResponse = await pinkService.ProcessAsync(Guid.NewGuid().ToString(), request);

        // Assert
        Assert.Equal("green-top", greenResponse.Outfit!.Items.First().Id);
        Assert.Equal("pink-top", pinkResponse.Outfit!.Items.First().Id);
    }

    [Fact]
    public async Task ProcessAsync_StandaloneLocationReply_ResolvesWeatherForThatCity()
    {
        // Arrange
        SetupWardrobe(CasualWomenWardrobe());
        SetupProfile(new UserProfileDto { Palette = ["#6B7A5E"] });
        SetupIntent(new OutfitIntentResult { Intent = "generate_outfit", IsInScope = false, OccasionText = "weekend", StyleBucket = "casual" });
        SetupCityWeather("Brasov", new ResolvedWeatherContext { Status = "cloudy", TemperatureC = 17, ThermalBand = "cool", Summary = "cloudy, cool, 17C" });

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(
            Msg("assistant", "Describe the occasion and weather, and I will build an outfit only from your Stylora wardrobe."),
            Msg("user", "Build me a weekend outfit."),
            Msg("assistant", "What weather should I dress for? You can describe it directly or tell me the city."),
            Msg("user", "Brasov")));

        // Assert
        Assert.Equal("success", response.Status);
        Assert.Contains("cloudy", response.Outfit!.WeatherSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_ShuffleMessageInConversation_ReturnsAlternativeOutfit()
    {
        // Arrange
        SetupWardrobe(
        [
            Item("green-top", "top", "shirt", color: "green"),
            Item("pink-top", "top", "shirt", color: "pink"),
            Item("jeans", "bottom", "jeans", color: "blue"),
            Item("sneakers", "shoes", "sneakers", color: "white")
        ]);
        SetupProfile(new UserProfileDto { Palette = ["#6B7A5E", "#F0A0B5"] });
        SetupIntent(CompleteIntent());
        SetupWeather(MildWeather());

        // Act
        var response = await _service.ProcessAsync(Guid.NewGuid().ToString(), Request(
            Msg("user", "Build me a casual outfit for today."),
            Msg("assistant", "I put together a casual look for weekend in sunny, warm, 22C weather."),
            Msg("user", "Shuffle another option")));

        // Assert
        Assert.Equal("success", response.Status);
        Assert.StartsWith("Here is another", response.AssistantMessage, StringComparison.Ordinal);
    }

    private OutfitChatService CreateServiceWithPalette(List<WardrobeItemDto> wardrobe, string paletteHex)
    {
        var wardrobeMock = new Mock<IWardrobeService>();
        wardrobeMock
            .Setup(w => w.GetAllItemsAsync(It.IsAny<string>()))
            .ReturnsAsync(wardrobe);
        var userMock = new Mock<IUserService>();
        userMock
            .Setup(u => u.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserProfileDto { Palette = [paletteHex] });
        var intentMock = new Mock<IOutfitIntentParser>();
        intentMock
            .Setup(p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompleteIntent());
        var weatherMock = new Mock<IWeatherService>();
        weatherMock
            .Setup(w => w.ResolveAsync(It.IsAny<OutfitIntentResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MildWeather());
        return new OutfitChatService(wardrobeMock.Object, userMock.Object, intentMock.Object, weatherMock.Object);
    }

    private void SetupWardrobe(IReadOnlyCollection<WardrobeItemDto> items)
        => _wardrobeService
            .Setup(w => w.GetAllItemsAsync(It.IsAny<string>()))
            .ReturnsAsync(items);

    private void SetupProfile(UserProfileDto profile)
        => _userService
            .Setup(u => u.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(profile);

    private void SetupIntent(OutfitIntentResult intent)
        => _intentParser
            .Setup(p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(intent);

    private void SetupWeather(ResolvedWeatherContext? weather)
        => _weatherService
            .Setup(w => w.ResolveAsync(It.IsAny<OutfitIntentResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(weather);

    private void SetupCityWeather(string city, ResolvedWeatherContext weather)
        => _weatherService
            .Setup(w => w.ResolveAsync(
                It.Is<OutfitIntentResult>(intent => string.Equals(intent.Location, city, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(weather);

    private static OutfitIntentResult CompleteIntent() => new()
    {
        Intent = "generate_outfit",
        IsInScope = true,
        OccasionText = "weekend",
        StyleBucket = "casual",
        WeatherStatus = "sunny",
        TemperatureC = 22
    };

    private static ResolvedWeatherContext MildWeather() => new()
    {
        Status = "sunny",
        TemperatureC = 22,
        ThermalBand = "warm",
        Summary = "sunny, warm, 22C"
    };

    private static List<WardrobeItemDto> CasualWomenWardrobe() =>
    [
        Item("top", "top", "shirt", color: "green"),
        Item("bottom", "bottom", "jeans", color: "blue"),
        Item("shoes", "shoes", "sneakers", color: "white")
    ];

    private static List<WardrobeItemDto> OfficeWomenWardrobe() =>
    [
        Item("1", "top", "shirt", color: "white", style: "office"),
        Item("2", "bottom", "trousers", color: "black", style: "formal"),
        Item("3", "shoes", "heels", color: "black", style: "elegant"),
        Item("4", "outerwear", "blazer", color: "black", style: "office")
    ];

    private static WardrobeItemDto Item(
        string id, string category, string? articleType, string? color = null, string style = "casual", string audience = "women") => new()
    {
        Id = id,
        Category = category,
        ArticleTypeLabel = articleType,
        Style = style,
        AudienceTag = audience,
        Image = $"{id}.png",
        Color = color,
        ValidationStatus = "pass"
    };

    private static OutfitChatRequest Request(params OutfitChatMessageDto[] messages) => new() { Messages = [.. messages] };

    private static OutfitChatMessageDto Msg(string role, string content) => new() { Role = role, Content = content };
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
