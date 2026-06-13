using FluentAssertions;
using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using TechTalk.SpecFlow;

namespace Stylora.Application.Tests.Features;

[Binding]
[Scope(Feature = "Season analysis service")]
public sealed class AnalysisServiceSteps
{
    private Mock<IGeminiService> _gemini = null!;
    private Mock<ISeasonAnalysisRepository> _analysisRepository = null!;
    private Mock<IUserRepository> _userRepository = null!;
    private AnalysisService _service = null!;
    private Guid _userGuid;
    private SeasonAnalysisResponse? _analysisResponse;
    private SeasonAnalysisResponse? _latestAnalysis;
    private Exception? _error;

    [BeforeScenario]
    public void Setup()
    {
        _gemini = new Mock<IGeminiService>();
        _analysisRepository = new Mock<ISeasonAnalysisRepository>();
        _userRepository = new Mock<IUserRepository>();
        _service = new AnalysisService(_gemini.Object, _analysisRepository.Object, _userRepository.Object);
    }

    [Given(@"Gemini classifies the photo as season ""(.*)"" sub-season ""(.*)"" with recommended colour ""(.*)""")]
    public void GivenGeminiClassifiesThePhoto(string season, string subSeason, string hex)
        => _gemini
            .Setup(g => g.AnalyzeSeasonAsync(It.IsAny<string>()))
            .ReturnsAsync(new SeasonAnalysisResult
            {
                Season = season,
                SubSeason = subSeason,
                BestMetals = "gold",
                RecommendedColors = [new RecommendedColor { Color = new Color { Name = hex, HexCode = hex } }]
            });

    [Given(@"a stored user exists")]
    public void GivenAStoredUserExists()
    {
        _userGuid = Guid.NewGuid();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(_userGuid))
            .ReturnsAsync(new User { Id = _userGuid, FirstName = "Ana" });
    }

    [When(@"a season analysis is requested")]
    public async Task WhenASeasonAnalysisIsRequested()
        => _analysisResponse = await _service.AnalyzeSeasonAsync(
            Guid.NewGuid().ToString(),
            new SeasonAnalysisRequest { ImageBase64 = "img-base64" });

    [When(@"the season profile is saved for user ""(.*)""")]
    public async Task WhenTheSeasonProfileIsSavedForUser(string userId)
    {
        try
        {
            await _service.SaveSeasonProfileAsync(userId, new SeasonAnalysisResponse { Season = "Nova" });
        }
        catch (Exception exception)
        {
            _error = exception;
        }
    }

    [When(@"the season profile is saved for that user")]
    public async Task WhenTheSeasonProfileIsSavedForThatUser()
        => await _service.SaveSeasonProfileAsync(_userGuid.ToString(), new SeasonAnalysisResponse
        {
            Season = "Nova",
            SubSeason = "Borealis",
            RecommendedColors = ["#101010"]
        });

    [When(@"the latest analysis is requested for user ""(.*)""")]
    public async Task WhenTheLatestAnalysisIsRequestedForUser(string userId)
        => _latestAnalysis = await _service.GetLatestAnalysisAsync(userId);

    [Then(@"the analysis season is ""(.*)""")]
    public void ThenTheAnalysisSeasonIs(string season) => _analysisResponse!.Season.Should().Be(season);

    [Then(@"the analysis palette contains ""(.*)""")]
    public void ThenTheAnalysisPaletteContains(string hex) => _analysisResponse!.RecommendedColors.Should().Contain(hex);

    [Then(@"the save is rejected as an invalid user")]
    public void ThenTheSaveIsRejected() => _error.Should().BeOfType<ArgumentException>();

    [Then(@"the analysis is upserted for that user")]
    public void ThenTheAnalysisIsUpserted()
        => _analysisRepository.Verify(
            r => r.UpsertAsync(It.Is<SeasonAnalysisResult>(a => a.UserId == _userGuid && a.Season == "Nova")),
            Times.Once);

    [Then(@"no analysis is returned")]
    public void ThenNoAnalysisIsReturned() => _latestAnalysis.Should().BeNull();

    [Then(@"the analysis repository was never queried")]
    public void ThenTheAnalysisRepositoryWasNeverQueried()
        => _analysisRepository.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
}

// Covers: BDD, Parameterized (Scenario Outline), Behaviour, Guard-clause
