using Moq;
using Stylora.Application.DTOs;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;
using Stylora.Domain.Entities;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class TryOnServiceTests
{
    private readonly Mock<IGeminiService> _gemini = new();
    private readonly Mock<ITryOnRepository> _tryOnRepository = new();
    private readonly TryOnService _service;

    public TryOnServiceTests()
    {
        _tryOnRepository
            .Setup(r => r.CreateAsync(It.IsAny<TryOnSession>()))
            .ReturnsAsync((TryOnSession session) => session);
        _tryOnRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TryOnSession>()))
            .ReturnsAsync((TryOnSession session) => session);
        _service = new TryOnService(_gemini.Object, _tryOnRepository.Object);
    }

    [Fact]
    public async Task GenerateTryOnAsync_GenerationSucceeds_ReturnsGeneratedImage()
    {
        // Arrange
        _gemini
            .Setup(g => g.GenerateTryOnAsync("person-img", "clothing-img"))
            .ReturnsAsync("generated-img");

        // Act
        var response = await _service.GenerateTryOnAsync(CreateRequest(), Guid.NewGuid());

        // Assert
        Assert.Equal("generated-img", response.GeneratedImage);
    }

    [Fact]
    public async Task GenerateTryOnAsync_GenerationSucceeds_PersistsSuccessfulSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _gemini
            .Setup(g => g.GenerateTryOnAsync("person-img", "clothing-img"))
            .ReturnsAsync("generated-img");

        // Act
        await _service.GenerateTryOnAsync(CreateRequest(), userId);

        // Assert
        _tryOnRepository.Verify(
            r => r.CreateAsync(It.Is<TryOnSession>(s => s.UserId == userId && s.PersonImagePath == "person-img")),
            Times.Once);
        _tryOnRepository.Verify(
            r => r.UpdateAsync(It.Is<TryOnSession>(s => s.IsSuccessful && s.GeneratedImagePath == "generated-img")),
            Times.Once);
    }

    [Fact]
    public async Task GenerateTryOnAsync_GeminiFails_MarksSessionFailedAndRethrows()
    {
        // Arrange
        _gemini
            .Setup(g => g.GenerateTryOnAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Gemini unavailable"));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GenerateTryOnAsync(CreateRequest(), Guid.NewGuid()));

        // Assert
        Assert.Equal("Gemini unavailable", exception.Message);
        _tryOnRepository.Verify(
            r => r.UpdateAsync(It.Is<TryOnSession>(s => !s.IsSuccessful && s.ErrorMessage == "Gemini unavailable")),
            Times.Once);
    }

    [Theory]
    [InlineData("person-a", "clothing-a")]
    [InlineData("person-b", "clothing-b")]
    [InlineData("person-c", "clothing-c")]
    public async Task GenerateTryOnAsync_ImagePairVariants_ForwardsExactImagesToGemini(string personImage, string clothingImage)
    {
        // Arrange
        _gemini
            .Setup(g => g.GenerateTryOnAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("generated-img");
        var request = new TryOnRequest { PersonImageBase64 = personImage, ClothingImageBase64 = clothingImage };

        // Act
        await _service.GenerateTryOnAsync(request, Guid.NewGuid());

        // Assert
        _gemini.Verify(g => g.GenerateTryOnAsync(personImage, clothingImage), Times.Once);
    }

    [Fact]
    public async Task GetLastPersonPhotoAsync_NoSessions_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _tryOnRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync([]);

        // Act
        var photo = await _service.GetLastPersonPhotoAsync(userId);

        // Assert
        Assert.Null(photo);
    }

    [Fact]
    public async Task GetLastPersonPhotoAsync_SessionsExist_ReturnsFirstSessionPersonImage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _tryOnRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(
            [
                new TryOnSession { PersonImagePath = "latest-photo" },
                new TryOnSession { PersonImagePath = "older-photo" }
            ]);

        // Act
        var photo = await _service.GetLastPersonPhotoAsync(userId);

        // Assert
        Assert.NotNull(photo);
        Assert.Equal("latest-photo", photo!.PersonImageBase64);
    }

    private static TryOnRequest CreateRequest() => new()
    {
        PersonImageBase64 = "person-img",
        ClothingImageBase64 = "clothing-img"
    };
}

// Covers: Unit, Parameterized, Behaviour, Error-path (service exposes no throwing guard clauses; null/empty edge cases asserted instead)
