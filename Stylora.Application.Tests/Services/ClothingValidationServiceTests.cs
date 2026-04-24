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
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c1", null, 0.08),
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c2", null, 0.12),
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c3", null, 0.15),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n1", null, 0.42),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n2", null, 0.48),
            ]),
            Settings);

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
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n1", null, 0.05),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n2", null, 0.08),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n3", null, 0.11),
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c1", null, 0.4),
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c2", null, 0.45),
            ]),
            Settings);

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
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c1", null, 0.19),
                new ClothingReferenceMatch(ClothingReferenceLabel.Clothing, "c2", null, 0.25),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n1", null, 0.21),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n2", null, 0.23),
                new ClothingReferenceMatch(ClothingReferenceLabel.NonClothing, "n3", null, 0.3),
            ]),
            Settings);

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
            Settings);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ValidateAsync("not-base64"));

        Assert.Equal("The uploaded image is not valid base64 data.", exception.Message);
    }

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
        private readonly IReadOnlyList<ClothingReferenceMatch> _matches;

        public FakeReferenceRepository(IReadOnlyList<ClothingReferenceMatch> matches)
        {
            _matches = matches;
        }

        public Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsAsync(float[] embedding, int count, CancellationToken cancellationToken = default)
            => Task.FromResult(_matches);
    }
}
