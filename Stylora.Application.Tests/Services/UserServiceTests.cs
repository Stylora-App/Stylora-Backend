using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_userRepository.Object);
    }

    [Fact]
    public async Task GetProfileAsync_UserWithAnalysisExists_ReturnsMappedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(userId))
            .ReturnsAsync(CreateUser(userId, "Autumn", "Deep Autumn"));

        // Act
        var profile = await _service.GetProfileAsync(userId.ToString());

        // Assert
        Assert.Equal("Ana", profile.FirstName);
        Assert.Equal("casual", profile.Style);
        Assert.Equal("Autumn", profile.Season);
        Assert.Equal("Warm", profile.Undertone);
        Assert.Equal("High", profile.Contrast);
        Assert.NotEmpty(profile.Palette);
    }

    [Fact]
    public async Task GetProfileAsync_InvalidUserId_ReturnsEmptyProfileWithoutQueryingRepository()
    {
        // Act
        var profile = await _service.GetProfileAsync("not-a-guid");

        // Assert
        Assert.Null(profile.FirstName);
        Assert.Null(profile.Season);
        _userRepository.Verify(r => r.GetByIdWithAnalysisAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProfileAsync_UserNotFound_ReturnsEmptyProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var profile = await _service.GetProfileAsync(userId.ToString());

        // Assert
        Assert.Null(profile.FirstName);
        Assert.Empty(profile.Palette);
    }

    [Fact]
    public async Task GetProfileAsync_AnalysisWithUnknownSeason_FallsBackToRecommendedColors()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "Nova", "Borealis");
        user.ColorAnalysisResult!.RecommendedColors =
        [
            new RecommendedColor { Color = new Color { Name = "#112233", HexCode = "#112233" } }
        ];
        _userRepository
            .Setup(r => r.GetByIdWithAnalysisAsync(userId))
            .ReturnsAsync(user);

        // Act
        var profile = await _service.GetProfileAsync(userId.ToString());

        // Assert
        Assert.Equal(["#112233"], profile.Palette);
        Assert.Null(profile.Undertone);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidUserId_ReturnsEmptyProfileWithoutPersisting()
    {
        // Act
        var profile = await _service.UpdateProfileAsync("invalid", new UpdateProfileRequest { FirstName = "Ana" });

        // Assert
        Assert.Null(profile.FirstName);
        _userRepository.Verify(
            r => r.UpdateProfileAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<StylePreference?>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Office", StylePreference.Office)]
    [InlineData("office", StylePreference.Office)]
    [InlineData("not-a-style", null)]
    [InlineData(null, null)]
    public async Task UpdateProfileAsync_StyleInputVariants_ParsesStyleCaseInsensitively(string? styleInput, StylePreference? expectedStyle)
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository
            .Setup(r => r.UpdateProfileAsync(userId, "New", "Name", "pic.png", expectedStyle))
            .ReturnsAsync(CreateUser(userId, "Autumn", "Deep Autumn"));

        // Act
        await _service.UpdateProfileAsync(userId.ToString(), new UpdateProfileRequest
        {
            FirstName = "New",
            LastName = "Name",
            ProfilePicture = "pic.png",
            Style = styleInput
        });

        // Assert
        _userRepository.Verify(r => r.UpdateProfileAsync(userId, "New", "Name", "pic.png", expectedStyle), Times.Once);
    }

    [Theory]
    [InlineData("Spring", "Warm")]
    [InlineData("Autumn", "Warm")]
    [InlineData("Summer", "Cool")]
    [InlineData("Winter", "Cool")]
    [InlineData("Unknown", null)]
    public void BuildProfileDto_SeasonVariants_DerivesUndertone(string season, string? expectedUndertone)
    {
        // Arrange
        var user = CreateUser(Guid.NewGuid(), season, subSeason: "");

        // Act
        var profile = UserService.BuildProfileDto(user);

        // Assert
        Assert.Equal(expectedUndertone, profile.Undertone);
    }

    [Theory]
    [InlineData("Soft Summer", "Low–medium")]
    [InlineData("Light Spring", "Low–medium")]
    [InlineData("Deep Autumn", "High")]
    [InlineData("True Winter", "High")]
    [InlineData("Warm Autumn", "Medium")]
    [InlineData(null, null)]
    public void BuildProfileDto_SubSeasonVariants_DerivesContrast(string? subSeason, string? expectedContrast)
    {
        // Arrange
        var user = CreateUser(Guid.NewGuid(), "Autumn", subSeason ?? "");
        if (subSeason is null)
        {
            user.ColorAnalysisResult!.SubSeason = null!;
        }

        // Act
        var profile = UserService.BuildProfileDto(user);

        // Assert
        Assert.Equal(expectedContrast, profile.Contrast);
    }

    [Fact]
    public void MapToUserDto_PopulatedUser_LowercasesStyleAndCopiesIdentity()
    {
        // Arrange
        var user = CreateUser(Guid.NewGuid(), "Autumn", "Deep Autumn");
        user.Email = "ana@stylora.app";

        // Act
        var dto = _service.MapToUserDto(user);

        // Assert
        Assert.Equal(user.Id, dto.Id);
        Assert.Equal("ana@stylora.app", dto.Email);
        Assert.Equal("casual", dto.Style);
    }

    private static User CreateUser(Guid id, string season, string subSeason) => new()
    {
        Id = id,
        Email = "ana@stylora.app",
        FirstName = "Ana",
        LastName = "Pop",
        Style = StylePreference.Casual,
        ColorAnalysisResult = new SeasonAnalysisResult
        {
            Season = season,
            SubSeason = subSeason,
            BestMetals = "gold"
        }
    };
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
