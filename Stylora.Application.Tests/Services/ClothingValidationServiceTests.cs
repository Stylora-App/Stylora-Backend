using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task ValidateAsync_ReturnsPass_WhenClothingNeighborsClearlyWin()
    {
        var service = new ClothingValidationService(
            new FakeImageEmbeddingService(),
            new FakeReferenceRepository(
            [
                Match(ClothingReferenceLabel.Clothing, "c1", 0.08),
                Match(ClothingReferenceLabel.Clothing, "c2", 0.12),
                Match(ClothingReferenceLabel.Clothing, "c3", 0.15),
                Match(ClothingReferenceLabel.NonClothing, "n1", 0.42),
                Match(ClothingReferenceLabel.NonClothing, "n2", 0.48),
            ]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal(ClothingValidationStatus.Pass, result.Status);
        Assert.True(result.IsLikelyClothing);
        Assert.True(result.Confidence >= 0.6);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarning_WhenNonClothingNeighborsWin()
    {
        var service = new ClothingValidationService(
            new FakeImageEmbeddingService(),
            new FakeReferenceRepository(
            [
                Match(ClothingReferenceLabel.NonClothing, "n1", 0.05),
                Match(ClothingReferenceLabel.NonClothing, "n2", 0.08),
                Match(ClothingReferenceLabel.NonClothing, "n3", 0.11),
                Match(ClothingReferenceLabel.Clothing, "c1", 0.4),
                Match(ClothingReferenceLabel.Clothing, "c2", 0.45),
            ]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.False(result.IsLikelyClothing);
        Assert.Contains("does not look like a clothing item", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarning_WhenNeighborsAreAmbiguous()
    {
        var service = new ClothingValidationService(
            new FakeImageEmbeddingService(),
            new FakeReferenceRepository(
            [
                Match(ClothingReferenceLabel.Clothing, "c1", 0.19),
                Match(ClothingReferenceLabel.Clothing, "c2", 0.25),
                Match(ClothingReferenceLabel.NonClothing, "n1", 0.21),
                Match(ClothingReferenceLabel.NonClothing, "n2", 0.23),
                Match(ClothingReferenceLabel.NonClothing, "n3", 0.3),
            ]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.True(result.Confidence < 0.6);
    }

    [Fact]
    public async Task ValidateAsync_PropagatesInvalidImageErrors()
    {
        var service = new ClothingValidationService(
            new ThrowingImageEmbeddingService(new ArgumentException("The uploaded image is not valid base64 data.")),
            new FakeReferenceRepository([]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ValidateAsync("not-base64"));

        Assert.Equal("The uploaded image is not valid base64 data.", exception.Message);
    }

    [Fact]
    public async Task ValidateAsync_FallsBackToScan_WhenVectorSearchReturnsNoNeighbors()
    {
        var service = new ClothingValidationService(
            new FakeImageEmbeddingService(),
            new FakeReferenceRepository(
                vectorMatches: [],
                scanMatches:
                [
                    Match(ClothingReferenceLabel.Clothing, "c1", 0.09, categoryGroup: "top"),
                    Match(ClothingReferenceLabel.Clothing, "c2", 0.12, categoryGroup: "top"),
                    Match(ClothingReferenceLabel.NonClothing, "n1", 0.4),
                ]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal(ClothingValidationStatus.Pass, result.Status);
        Assert.True(result.IsLikelyClothing);
        Assert.Equal("top", result.SuggestedCategory);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsMetadataSuggestions_FromClothingNeighbors()
    {
        var service = new ClothingValidationService(
            new FakeImageEmbeddingService(),
            new FakeReferenceRepository(
            [
                Match(ClothingReferenceLabel.Clothing, "c1", 0.08, categoryGroup: "top", baseColour: "navy blue", colorFamily: "blue", usageTag: "casual", genderTag: "men"),
                Match(ClothingReferenceLabel.Clothing, "c2", 0.12, categoryGroup: "top", baseColour: "navy blue", colorFamily: "blue", usageTag: "casual", genderTag: "men"),
                Match(ClothingReferenceLabel.Clothing, "c3", 0.2, categoryGroup: "outerwear", baseColour: "black", colorFamily: "black", usageTag: "formal", genderTag: "men"),
                Match(ClothingReferenceLabel.NonClothing, "n1", 0.43),
                Match(ClothingReferenceLabel.NonClothing, "n2", 0.49),
            ]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal("top", result.SuggestedCategory);
        Assert.Equal("casual", result.SuggestedStyle);
        Assert.Equal("Navy Blue", result.SuggestedColor);
        Assert.Equal("blue", result.SuggestedColorFamily);
        Assert.Equal("casual", result.SuggestedUsage);
        Assert.Equal("men", result.SuggestedGender);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarning_WhenEmbeddingWorkerIsStillStarting()
    {
        var service = new ClothingValidationService(
            new ThrowingImageEmbeddingService(new TimeoutException("Worker startup timed out.")),
            new FakeReferenceRepository([]),
            Settings,
            NullLogger<ClothingValidationService>.Instance);

        var result = await service.ValidateAsync("data:image/png;base64,aGVsbG8=");

        Assert.Equal(ClothingValidationStatus.Warning, result.Status);
        Assert.False(result.IsLikelyClothing);
        Assert.Contains("warming up", result.Message);
    }

    private static ClothingReferenceMatch Match(
        ClothingReferenceLabel label,
        string sourceKey,
        double distance,
        string? categoryGroup = null,
        string? articleType = null,
        string? baseColour = null,
        string? colorFamily = null,
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
            baseColour,
            colorFamily,
            null,
            usageTag,
            null,
            distance);

    private sealed class FakeImageEmbeddingService : IImageEmbeddingService
    {
        public Task<float[]> EmbedImageAsync(string imageBase64, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { 0.1f, 0.2f, 0.3f });
    }

    private sealed class ThrowingImageEmbeddingService : IImageEmbeddingService
    {
        private readonly Exception _exception;

        public ThrowingImageEmbeddingService(Exception exception)
        {
            _exception = exception;
        }

        public Task<float[]> EmbedImageAsync(string imageBase64, CancellationToken cancellationToken = default)
            => Task.FromException<float[]>(_exception);
    }

    private sealed class FakeReferenceRepository : IClothingReferenceEmbeddingRepository
    {
        private readonly IReadOnlyList<ClothingReferenceMatch> _vectorMatches;
        private readonly IReadOnlyList<ClothingReferenceMatch> _scanMatches;

        public FakeReferenceRepository(
            IReadOnlyList<ClothingReferenceMatch> vectorMatches,
            IReadOnlyList<ClothingReferenceMatch>? scanMatches = null)
        {
            _vectorMatches = vectorMatches;
            _scanMatches = scanMatches ?? vectorMatches;
        }

        public Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsAsync(float[] embedding, int count, CancellationToken cancellationToken = default)
            => Task.FromResult(_vectorMatches);

        public Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsByScanAsync(float[] embedding, int count, CancellationToken cancellationToken = default)
            => Task.FromResult(_scanMatches);
    }
}
