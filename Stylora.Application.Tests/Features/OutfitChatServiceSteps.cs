using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Outfit chat service")]
public sealed class OutfitChatServiceSteps
{
    private Mock<IWardrobeService> _wardrobeService = null!;
    private Mock<IUserService> _userService = null!;
    private Mock<IOutfitIntentParser> _intentParser = null!;
    private Mock<IWeatherService> _weatherService = null!;
    private readonly List<OutfitChatMessageDto> _messages = [];
    private OutfitChatResponse? _response;

    [BeforeScenario]
    public void Setup()
    {
        _wardrobeService = new Mock<IWardrobeService>();
        _userService = new Mock<IUserService>();
        _intentParser = new Mock<IOutfitIntentParser>();
        _weatherService = new Mock<IWeatherService>();
        _wardrobeService
            .Setup(w => w.GetAllItemsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _userService
            .Setup(u => u.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserProfileDto { Palette = ["#6B7A5E"] });
        // The parser yields a bare intent so the conversation-enrichment logic under test does the work.
        _intentParser
            .Setup(p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutfitIntentResult { Intent = "generate_outfit", IsInScope = false });
    }

    [Given(@"the intent parser finds no outfit request")]
    public void GivenTheIntentParserFindsNoOutfitRequest()
        => _intentParser
            .Setup(p => p.ParseAsync(It.IsAny<IReadOnlyList<OutfitChatMessageDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutfitIntentResult { Intent = "out_of_scope", IsInScope = false });

    [Given(@"any weather resolves to ""(.*)"" at (\d+) degrees")]
    public void GivenAnyWeatherResolvesTo(string status, int temperature)
        => _weatherService
            .Setup(w => w.ResolveAsync(It.IsAny<OutfitIntentResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Weather(status, temperature));

    [Given(@"the city ""(.*)"" has ""(.*)"" weather at (\d+) degrees")]
    public void GivenTheCityHasWeather(string city, string status, int temperature)
        => _weatherService
            .Setup(w => w.ResolveAsync(
                It.Is<OutfitIntentResult>(intent => string.Equals(intent.Location, city, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Weather(status, temperature));

    [Given(@"my wardrobe contains a complete casual women wardrobe")]
    public void GivenMyWardrobeContainsACompleteCasualWomenWardrobe()
        => _wardrobeService
            .Setup(w => w.GetAllItemsAsync(It.IsAny<string>()))
            .ReturnsAsync(
            [
                Item("top", "top", "shirt", "green"),
                Item("bottom", "bottom", "jeans", "blue"),
                Item("shoes", "shoes", "sneakers", "white")
            ]);

    [Given(@"the user says ""(.*)""")]
    public void GivenTheUserSays(string message)
        => _messages.Add(new OutfitChatMessageDto { Role = "user", Content = message });

    [When(@"the outfit chat processes the conversation")]
    public async Task WhenTheOutfitChatProcessesTheConversation()
    {
        var service = new OutfitChatService(
            _wardrobeService.Object,
            _userService.Object,
            _intentParser.Object,
            _weatherService.Object);
        _response = await service.ProcessAsync(Guid.NewGuid().ToString(), new OutfitChatRequest { Messages = _messages });
    }

    [Then(@"the chat status is ""(.*)""")]
    public void ThenTheChatStatusIs(string status) => _response!.Status.Should().Be(status);

    [Then(@"the missing fields include ""(.*)""")]
    public void ThenTheMissingFieldsInclude(string field) => _response!.MissingFields.Should().Contain(field);

    [Then(@"the outfit weather summary contains ""(.*)""")]
    public void ThenTheOutfitWeatherSummaryContains(string text)
    {
        _response!.Outfit.Should().NotBeNull();
        _response.Outfit!.WeatherSummary.Should().ContainEquivalentOf(text);
    }

    private static ResolvedWeatherContext Weather(string status, int temperature) => new()
    {
        Status = status,
        TemperatureC = temperature,
        ThermalBand = temperature <= 18 ? "cool" : "warm",
        Summary = $"{status}, {temperature}C"
    };

    private static WardrobeItemDto Item(string id, string category, string articleType, string color) => new()
    {
        Id = id,
        Category = category,
        ArticleTypeLabel = articleType,
        Style = "casual",
        AudienceTag = "women",
        Image = $"{id}.png",
        Color = color,
        ValidationStatus = "pass"
    };
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
