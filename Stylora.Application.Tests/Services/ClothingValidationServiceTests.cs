using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Domain.Enums;
using Xunit;

namespace Stylora.Application.Tests.Services;

public class ClothingValidationServiceTests
{
    private static readonly ClothingValidationSettings Settings = new()
    {
        TopK = 5,
        MinimumClothingShare = 0.6,
        MinimumMargin = 0.15
    };

    private readonly Mock<IImageEmbeddingService> _embeddingService = new();
    private readonly Mock<IClothingReferenceEmbeddingRepository> _referenceRepository = new();
    private readonly ClothingValidationService _service;

    public ClothingValidationServiceTests()
    {
        _embeddingService
            .Setup(s => s.EmbedImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        _service = new ClothingValidationService(
            _embeddingService.Object,
            _referenceRepository.Object,
            Settings,
            NullLogger<ClothingValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAsync_ClothingNeighborsClearlyWin_ReturnsPass()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.Clothing, "c1", 0.08),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.12),
            Match(ClothingReferenceLabel.Clothing, "c3", 0.15),
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.42),
            Match(ClothingReferenceLabel.NonClothing, "n2", 0.48),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(ClothingValidationStatus.Pass, result.Status);
        Assert.True(result.IsLikelyClothing);
        Assert.True(result.Confidence >= 0.6);
    }

    [Fact]
    public async Task ValidateAsync_NonClothingNeighborsWin_ReturnsWarning()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.05),
            Match(ClothingReferenceLabel.NonClothing, "n2", 0.08),
            Match(ClothingReferenceLabel.NonClothing, "n3", 0.11),
            Match(ClothingReferenceLabel.Clothing, "c1", 0.4),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.45),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.False(result.IsLikelyClothing);
        Assert.Contains("does not look like a clothing item", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_NeighborsAreAmbiguous_ReturnsWarningWithLowConfidence()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.Clothing, "c1", 0.19),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.25),
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.21),
            Match(ClothingReferenceLabel.NonClothing, "n2", 0.23),
            Match(ClothingReferenceLabel.NonClothing, "n3", 0.3),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.True(result.Confidence < 0.6);
    }

    [Theory]
    [InlineData(5, 0, ClothingValidationStatus.Pass)]
    [InlineData(3, 2, ClothingValidationStatus.Pass)]
    [InlineData(2, 3, ClothingValidationStatus.Warning)]
    [InlineData(0, 5, ClothingValidationStatus.Warning)]
    public async Task ValidateAsync_NeighborLabelMixVariants_AppliesShareAndMarginThresholds(
        int clothingCount, int nonClothingCount, ClothingValidationStatus expectedStatus)
    {
        // Arrange: equal distances make clothing share = clothing / total
        var matches = Enumerable.Range(0, clothingCount)
            .Select(i => Match(ClothingReferenceLabel.Clothing, $"c{i}", 0.2))
            .Concat(Enumerable.Range(0, nonClothingCount)
                .Select(i => Match(ClothingReferenceLabel.NonClothing, $"n{i}", 0.2)))
            .ToList();
        SetupNeighbors(matches);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_EmbeddingRejectsImage_PropagatesArgumentException()
    {
        // Arrange
        _embeddingService
            .Setup(s => s.EmbedImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The uploaded image is not valid base64 data."));

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.ValidateAsync("not-base64"));

        // Assert
        Assert.Equal("The uploaded image is not valid base64 data.", exception.Message);
        _referenceRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidateAsync_EmbeddingWorkerStillStarting_ReturnsWarningInsteadOfFailing()
    {
        // Arrange
        _embeddingService
            .Setup(s => s.EmbedImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Worker startup timed out."));

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.False(result.IsLikelyClothing);
        Assert.Contains("warming up", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_VectorSearchReturnsNoNeighbors_FallsBackToScan()
    {
        // Arrange
        SetupNeighbors(
            vectorMatches: [],
            scanMatches:
            [
                Match(ClothingReferenceLabel.Clothing, "c1", 0.09, categoryGroup: "top"),
                Match(ClothingReferenceLabel.Clothing, "c2", 0.12, categoryGroup: "top"),
                Match(ClothingReferenceLabel.NonClothing, "n1", 0.4),
            ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal(ClothingValidationStatus.Pass, result.Status);
        Assert.Equal("top", result.SuggestedCategory);
        _referenceRepository.Verify(
            r => r.GetNearestNeighborsByScanAsync(It.IsAny<float[]>(), Settings.TopK, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ClothingNeighborsCarryMetadata_ReturnsMetadataSuggestions()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.Clothing, "c1", 0.08, categoryGroup: "top", usageTag: "casual", genderTag: "men"),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.12, categoryGroup: "top", usageTag: "casual", genderTag: "men"),
            Match(ClothingReferenceLabel.Clothing, "c3", 0.2, categoryGroup: "outerwear", usageTag: "formal", genderTag: "men"),
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.43),
            Match(ClothingReferenceLabel.NonClothing, "n2", 0.49),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal("top", result.SuggestedCategory);
        Assert.Equal("casual", result.SuggestedStyle);
        Assert.Equal("casual", result.SuggestedUsage);
        Assert.Equal("men", result.SuggestedGender);
    }

    [Fact]
    public async Task ValidateAsync_EthnicUsageNeighborsPresent_IgnoresExcludedUsageForSuggestions()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.Clothing, "c1", 0.05, categoryGroup: "top", articleType: "kurtas", usageTag: "ethnic", genderTag: "women"),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.08, categoryGroup: "dress", articleType: "dresses", usageTag: "casual", genderTag: "women"),
            Match(ClothingReferenceLabel.Clothing, "c3", 0.11, categoryGroup: "dress", articleType: "dresses", usageTag: "casual", genderTag: "women"),
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.45),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal("dress", result.SuggestedCategory);
        Assert.Equal("dress", result.SuggestedArticleType);
        Assert.Equal("one_piece", result.SuggestedOutfitRole);
    }

    [Fact]
    public async Task ValidateAsync_MenSignalClearlyWins_PrefersMenOverUnisex()
    {
        // Arrange
        SetupNeighbors(
        [
            Match(ClothingReferenceLabel.Clothing, "c1", 0.06, categoryGroup: "top", articleType: "shirts", usageTag: "casual", genderTag: "men"),
            Match(ClothingReferenceLabel.Clothing, "c2", 0.07, categoryGroup: "top", articleType: "shirts", usageTag: "casual", genderTag: "men"),
            Match(ClothingReferenceLabel.Clothing, "c3", 0.10, categoryGroup: "top", articleType: "shirts", usageTag: "casual", genderTag: "unisex"),
            Match(ClothingReferenceLabel.NonClothing, "n1", 0.40),
        ]);

        // Act
        var result = await _service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        // Assert
        Assert.Equal("men", result.SuggestedGender);
    }

    private void SetupNeighbors(
        IReadOnlyList<ClothingReferenceMatch> vectorMatches,
        IReadOnlyList<ClothingReferenceMatch>? scanMatches = null)
    {
        _referenceRepository
            .Setup(r => r.GetNearestNeighborsAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorMatches);
        _referenceRepository
            .Setup(r => r.GetNearestNeighborsByScanAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanMatches ?? vectorMatches);
    }

    private static ClothingReferenceMatch Match(
        ClothingReferenceLabel label,
        string sourceKey,
        double distance,
        string? categoryGroup = null,
        string? articleType = null,
        string? usageTag = null,
        string? genderTag = null)
        => new(
            label,
            sourceKey,
            null,
            genderTag,
            null,
            null,
            articleType,
            categoryGroup,
            null,
            null,
            null,
            usageTag,
            null,
            distance);
}

// Covers: Unit, Parameterized, Behaviour, Guard-clause
