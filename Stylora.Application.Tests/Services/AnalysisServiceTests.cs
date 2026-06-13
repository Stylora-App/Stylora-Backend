using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class AnalysisServiceTests
{
    private readonly Mock<IGeminiService> _gemini = new();
    private readonly Mock<ISeasonAnalysisRepository> _analysisRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly AnalysisService _service;

    public AnalysisServiceTests()
    {
        _service = new AnalysisService(_gemini.Object, _analysisRepository.Object, _userRepository.Object);
    }

    [Fact]
    public async Task AnalyzeSeasonAsync_GeminiReturnsResult_MapsAllAnnotationFields()
    {
        // Arrange
        _gemini
            .Setup(g => g.AnalyzeSeasonAsync("img-base64"))
            .ReturnsAsync(CreateAnalysis(season: "Nova", subSeason: "Borealis", hex: "#112233"));

        // Act
        var response = await _service.AnalyzeSeasonAsync(Guid.NewGuid().ToString(), new SeasonAnalysisRequest { ImageBase64 = "img-base64" });

        // Assert
        Assert.Equal("Nova", response.Season);
        Assert.Equal("Borealis", response.SubSeason);
        Assert.Equal(["#112233"], response.RecommendedColors);
        Assert.Equal("gold", response.BestMetals);
        Assert.Equal("brown", response.HairColor);
        Assert.Equal("Warm", response.Undertone);
    }

    [Fact]
    public async Task AnalyzeSeasonAsync_ImageProvided_ForwardsExactImageToGemini()
    {
        // Arrange
        _gemini
            .Setup(g => g.AnalyzeSeasonAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateAnalysis("Nova", "Borealis", "#112233"));

        // Act
        await _service.AnalyzeSeasonAsync(Guid.NewGuid().ToString(), new SeasonAnalysisRequest { ImageBase64 = "exact-image" });

        // Assert
        _gemini.Verify(g => g.AnalyzeSeasonAsync("exact-image"), Times.Once);
        _gemini.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public async Task GetLatestAnalysisAsync_InvalidUserId_ReturnsNullWithoutQueryingRepository(string userId)
    {
        // Act
        var response = await _service.GetLatestAnalysisAsync(userId);

        // Assert
        Assert.Null(response);
        _analysisRepository.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetLatestAnalysisAsync_NoStoredAnalysis_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _analysisRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync((SeasonAnalysisResult?)null);

        // Act
        var response = await _service.GetLatestAnalysisAsync(userId.ToString());

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task GetLatestAnalysisAsync_StoredAnalysisExists_MapsIdAndPalette()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var stored = CreateAnalysis("Nova", "Borealis", "#445566");
        stored.Id = Guid.NewGuid();
        _analysisRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(stored);

        // Act
        var response = await _service.GetLatestAnalysisAsync(userId.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(stored.Id.ToString(), response!.Id);
        Assert.Equal(["#445566"], response.RecommendedColors);
    }

    [Fact]
    public async Task SaveSeasonProfileAsync_InvalidUserId_ThrowsArgumentException()
    {
        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveSeasonProfileAsync("not-a-guid", new SeasonAnalysisResponse()));

        // Assert
        Assert.Equal("userId", exception.ParamName);
        _analysisRepository.Verify(r => r.UpsertAsync(It.IsAny<SeasonAnalysisResult>()), Times.Never);
    }

    [Fact]
    public async Task SaveSeasonProfileAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(userId))
            .ReturnsAsync((User?)null);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SaveSeasonProfileAsync(userId.ToString(), new SeasonAnalysisResponse { Season = "Nova" }));
    }

    [Fact]
    public async Task SaveSeasonProfileAsync_ValidUser_UpsertsAnalysisAndReturnsProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(userId))
            .ReturnsAsync(new User { Id = userId, FirstName = "Ana" });
        var analysis = new SeasonAnalysisResponse
        {
            Season = "Nova",
            SubSeason = "Borealis",
            RecommendedColors = ["#101010"]
        };

        // Act
        var profile = await _service.SaveSeasonProfileAsync(userId.ToString(), analysis);

        // Assert
        Assert.Equal("Ana", profile.FirstName);
        _analysisRepository.Verify(
            r => r.UpsertAsync(It.Is<SeasonAnalysisResult>(a =>
                a.UserId == userId &&
                a.Season == "Nova" &&
                a.RecommendedColors.Single().Color!.HexCode == "#101010")),
            Times.Once);
    }

    private static SeasonAnalysisResult CreateAnalysis(string season, string subSeason, string hex) => new()
    {
        Season = season,
        SubSeason = subSeason,
        Description = "A vivid palette.",
        BestMetals = "gold",
        HairColor = "brown",
        Undertone = "Warm",
        RecommendedColors =
        [
            new RecommendedColor { Color = new Color { Name = hex, HexCode = hex } }
        ]
    };
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
