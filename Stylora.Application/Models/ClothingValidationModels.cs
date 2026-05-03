using Stylora.Domain.Enums;

namespace Stylora.Application.Models;

public sealed class ClothingValidationSettings
{
    public int TopK { get; set; } = 5;
    public double MinimumClothingShare { get; set; } = 0.6;
    public double MinimumMargin { get; set; } = 0.15;
    public string SeedDirectoryPath { get; set; } = "SeedData/clothing-validation";
    public string DatasetDirectoryName { get; set; } = "fashion-product-images-small";
    public string DatasetMetadataFileName { get; set; } = "seed_manifest.csv";
    public string DatasetImageDirectoryName { get; set; } = "images";
    public string LegacyDatasetDirectoryName { get; set; } = "clothing_dataset";
    public string LegacyDatasetMetadataFileName { get; set; } = "images.csv";
    public string LegacyDatasetImageDirectoryName { get; set; } = "images_compressed";
    public int SeedSaveBatchSize { get; set; } = 25;
    public List<string> DatasetExcludedLabels { get; set; } = ["Skip"];
    public string PythonExecutablePath { get; set; } = "../../armochromia_classifier/.venv/Scripts/python.exe";
    public string WorkerScriptPath { get; set; } = "Python/clip_image_embedding_worker.py";
    public string ModelId { get; set; } = "openai/clip-vit-base-patch32";
    public int WorkerStartupTimeoutSeconds { get; set; } = 600;
    public bool WarmupWorkerOnStartup { get; set; } = true;
    public int EmbeddingDimensions { get; set; } = 512;
}

public sealed record ClothingReferenceMatch(
    ClothingReferenceLabel Label,
    string SourceKey,
    string? CategoryHint,
    string? GenderTag,
    string? MasterCategory,
    string? SubCategory,
    string? ArticleType,
    string? CategoryGroup,
    string? BaseColour,
    string? ColorFamily,
    string? SeasonTag,
    string? UsageTag,
    string? DisplayName,
    double Distance);

public sealed class ClothingImageValidationResult
{
    public ClothingValidationStatus Status { get; init; }
    public bool IsLikelyClothing { get; init; }
    public double Confidence { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> NearestLabels { get; init; } = Array.Empty<string>();
    public string? SuggestedCategory { get; init; }
    public string? SuggestedArticleType { get; init; }
    public string? SuggestedStyle { get; init; }
    public string? SuggestedColor { get; init; }
    public string? SuggestedColorFamily { get; init; }
    public string? SuggestedUsage { get; init; }
    public string? SuggestedGender { get; init; }
}
