using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Enums;

namespace Stylora.Application.Services;

public class ClothingValidationService : IClothingValidationService
{
    private readonly IImageEmbeddingService _imageEmbeddingService;
    private readonly IClothingReferenceEmbeddingRepository _referenceRepository;
    private readonly ClothingValidationSettings _settings;

    public ClothingValidationService(
        IImageEmbeddingService imageEmbeddingService,
        IClothingReferenceEmbeddingRepository referenceRepository,
        ClothingValidationSettings settings)
    {
        _imageEmbeddingService = imageEmbeddingService;
        _referenceRepository = referenceRepository;
        _settings = settings;
    }

    public async Task<ClothingImageValidationResult> ValidateAsync(string imageBase64, CancellationToken cancellationToken = default)
    {
        var embedding = await _imageEmbeddingService.EmbedImageAsync(imageBase64, cancellationToken);
        var neighbors = await _referenceRepository.GetNearestNeighborsAsync(embedding, _settings.TopK, cancellationToken);

        if (neighbors.Count == 0)
        {
            return new ClothingImageValidationResult
            {
                Status = ClothingValidationStatus.Warning,
                IsLikelyClothing = false,
                Confidence = 0,
                Message = "Clothing validation is not ready yet for this image, but you can still save it.",
                NearestLabels = []
            };
        }

        var clothingScore = Score(neighbors, ClothingReferenceLabel.Clothing);
        var nonClothingScore = Score(neighbors, ClothingReferenceLabel.NonClothing);
        var totalScore = clothingScore + nonClothingScore;
        var clothingShare = totalScore <= 0 ? 0.5 : clothingScore / totalScore;
        var margin = Math.Abs(clothingShare - 0.5d) * 2d;
        var isLikelyClothing = clothingScore >= nonClothingScore;
        var confidence = Math.Clamp(isLikelyClothing ? clothingShare : 1d - clothingShare, 0d, 1d);

        var status = isLikelyClothing &&
                     clothingShare >= _settings.MinimumClothingShare &&
                     margin >= _settings.MinimumMargin
            ? ClothingValidationStatus.Pass
            : ClothingValidationStatus.Warning;

        var message = status == ClothingValidationStatus.Pass
            ? "This upload looks like a clothing item."
            : isLikelyClothing
                ? "This image might be a clothing item, but the validator is not confident. You can still save it."
                : "This image does not look like a clothing item to the validator. You can still save it if that is intentional.";

        return new ClothingImageValidationResult
        {
            Status = status,
            IsLikelyClothing = isLikelyClothing,
            Confidence = Math.Round(confidence, 4),
            Message = message,
            NearestLabels = neighbors.Select(match => match.Label == ClothingReferenceLabel.Clothing ? "clothing" : "non_clothing").ToList()
        };
    }

    private static double Score(IEnumerable<ClothingReferenceMatch> neighbors, ClothingReferenceLabel label)
    {
        return neighbors
            .Where(match => match.Label == label)
            .Select(match => Math.Max(0d, 1d - match.Distance))
            .DefaultIfEmpty(0d)
            .Sum();
    }
}
