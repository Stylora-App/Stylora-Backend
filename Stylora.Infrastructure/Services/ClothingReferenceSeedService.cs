using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Services;

public class ClothingReferenceSeedService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp"
    };

    private static readonly HashSet<string> AllowedPositiveGenders = new(StringComparer.OrdinalIgnoreCase)
    {
        "men",
        "women",
        "unisex"
    };

    private static readonly HashSet<string> AllowedPositiveMasterCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "apparel",
        "footwear",
        "accessories"
    };

    private readonly StyloraDbContext _context;
    private readonly IImageEmbeddingService _embeddingService;
    private readonly ClothingValidationSettings _settings;
    private readonly ILogger<ClothingReferenceSeedService> _logger;

    public ClothingReferenceSeedService(
        StyloraDbContext context,
        IImageEmbeddingService embeddingService,
        ClothingValidationSettings settings,
        ILogger<ClothingReferenceSeedService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _settings = settings;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var root = ResolvePath(_settings.SeedDirectoryPath);
        _logger.LogInformation("Starting clothing reference seeding from {Path}", root);
        if (!Directory.Exists(root))
        {
            _logger.LogWarning("Clothing validation seed directory was not found at {Path}", root);
            return;
        }

        var existingKeys = await _context.ClothingReferenceEmbeddings
            .Select(reference => reference.SourceKey)
            .ToHashSetAsync(cancellationToken);

        var seededCount = 0;
        seededCount += await SeedFolderAsync(root, "non_clothing", ClothingReferenceLabel.NonClothing, existingKeys, cancellationToken);

        await DeactivateLegacyPositiveReferencesAsync(root, cancellationToken);
        var (primaryAvailable, primarySeeded) = await SeedPrimaryDatasetAsync(root, existingKeys, cancellationToken);
        seededCount += primarySeeded;

        if (!primaryAvailable)
        {
            var (legacyAvailable, legacySeeded) = await SeedLegacyDatasetAsync(root, existingKeys, cancellationToken);
            seededCount += legacySeeded;

            if (!legacyAvailable)
            {
                seededCount += await SeedFolderAsync(root, "clothing", ClothingReferenceLabel.Clothing, existingKeys, cancellationToken);
            }
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Clothing reference seeding finished. Added {Count} embeddings.", seededCount);
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private async Task<(bool DatasetAvailable, int AddedCount)> SeedPrimaryDatasetAsync(
        string root,
        HashSet<string> existingKeys,
        CancellationToken cancellationToken)
    {
        var datasetRoot = Path.Combine(root, _settings.DatasetDirectoryName);
        var metadataPath = Path.Combine(datasetRoot, _settings.DatasetMetadataFileName);
        var imageDirectory = Path.Combine(datasetRoot, _settings.DatasetImageDirectoryName);

        if (!Directory.Exists(datasetRoot) || !File.Exists(metadataPath) || !Directory.Exists(imageDirectory))
        {
            return (false, 0);
        }

        var added = 0;
        foreach (var line in File.ReadLines(metadataPath).Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = ParsePrimaryDatasetRow(line);
            if (row is null ||
                !AllowedPositiveGenders.Contains(row.GenderTag) ||
                !AllowedPositiveMasterCategories.Contains(row.MasterCategory))
            {
                continue;
            }

            var filePath = Path.Combine(imageDirectory, row.ImagePath);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var sourceKey = $"{_settings.DatasetDirectoryName}/{_settings.DatasetImageDirectoryName}/{row.ImagePath}";
            if (!existingKeys.Add(sourceKey))
            {
                continue;
            }

            if (added == 0)
            {
                _logger.LogInformation("Embedding first fashion seed reference from {SourceKey}", sourceKey);
            }

            await AddEmbeddingAsync(
                filePath,
                sourceKey,
                ClothingReferenceLabel.Clothing,
                BuildPrimaryReferenceMetadata(row),
                cancellationToken);

            added++;
            await FlushBatchAsync(added, cancellationToken);
        }

        if (added > 0)
        {
            _logger.LogInformation("Seeded {Count} clothing embeddings from primary fashion dataset {DatasetRoot}", added, datasetRoot);
        }

        return (true, added);
    }

    private async Task DeactivateLegacyPositiveReferencesAsync(string root, CancellationToken cancellationToken)
    {
        var primaryDatasetRoot = Path.Combine(root, _settings.DatasetDirectoryName);
        if (!Directory.Exists(primaryDatasetRoot))
        {
            return;
        }

        var updated = await _context.ClothingReferenceEmbeddings
            .Where(reference => reference.IsActive &&
                                reference.Label == ClothingReferenceLabel.Clothing &&
                                (reference.SourceKey.StartsWith($"{_settings.LegacyDatasetDirectoryName}/") ||
                                 reference.SourceKey.StartsWith("clothing/")))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(reference => reference.IsActive, false)
                .SetProperty(reference => reference.UpdatedAt, DateTime.UtcNow), cancellationToken);

        if (updated > 0)
        {
            _logger.LogInformation(
                "Deactivated {Count} legacy positive clothing references because the primary fashion dataset is available.",
                updated);
        }
    }

    private async Task<(bool DatasetAvailable, int AddedCount)> SeedLegacyDatasetAsync(
        string root,
        HashSet<string> existingKeys,
        CancellationToken cancellationToken)
    {
        var datasetRoot = Path.Combine(root, _settings.LegacyDatasetDirectoryName);
        var metadataPath = Path.Combine(datasetRoot, _settings.LegacyDatasetMetadataFileName);
        var imageDirectory = Path.Combine(datasetRoot, _settings.LegacyDatasetImageDirectoryName);

        if (!Directory.Exists(datasetRoot) || !File.Exists(metadataPath) || !Directory.Exists(imageDirectory))
        {
            return (false, 0);
        }

        var excludedLabels = _settings.DatasetExcludedLabels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var line in File.ReadLines(metadataPath).Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = ParseLegacyDatasetRow(line);
            if (row is null || excludedLabels.Contains(row.Label))
            {
                continue;
            }

            var filePath = Path.Combine(imageDirectory, $"{row.ImageId}.jpg");
            if (!File.Exists(filePath))
            {
                continue;
            }

            var sourceKey = $"{_settings.LegacyDatasetDirectoryName}/{_settings.LegacyDatasetImageDirectoryName}/{row.ImageId}.jpg";
            if (!existingKeys.Add(sourceKey))
            {
                continue;
            }

            if (added == 0)
            {
                _logger.LogInformation("Embedding first legacy clothing reference from {SourceKey}", sourceKey);
            }

            await AddEmbeddingAsync(
                filePath,
                sourceKey,
                ClothingReferenceLabel.Clothing,
                BuildLegacyReferenceMetadata(row),
                cancellationToken);

            added++;
            await FlushBatchAsync(added, cancellationToken);
        }

        if (added > 0)
        {
            _logger.LogInformation("Seeded {Count} clothing embeddings from legacy dataset {DatasetRoot}", added, datasetRoot);
        }

        return (true, added);
    }

    private async Task<int> SeedFolderAsync(
        string root,
        string folderName,
        ClothingReferenceLabel label,
        HashSet<string> existingKeys,
        CancellationToken cancellationToken)
    {
        var folderPath = Path.Combine(root, folderName);
        if (!Directory.Exists(folderPath))
        {
            return 0;
        }

        var added = 0;
        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                     .Where(path => SupportedExtensions.Contains(Path.GetExtension(path))))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceKey = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            if (!existingKeys.Add(sourceKey))
            {
                continue;
            }

            if (added == 0)
            {
                _logger.LogInformation("Embedding first {Label} reference from {SourceKey}", label, sourceKey);
            }

            var categoryHint = ResolveCategoryHint(folderPath, filePath);
            await AddEmbeddingAsync(
                filePath,
                sourceKey,
                label,
                new ReferenceMetadata
                {
                    CategoryHint = categoryHint,
                    SourceDataset = folderName,
                    CategoryGroup = label == ClothingReferenceLabel.NonClothing ? "non_clothing" : MapLegacyCategoryGroup(categoryHint),
                    ArticleType = NormalizeValue(categoryHint)
                },
                cancellationToken);

            added++;
            await FlushBatchAsync(added, cancellationToken);
        }

        return added;
    }

    private async Task AddEmbeddingAsync(
        string filePath,
        string sourceKey,
        ClothingReferenceLabel label,
        ReferenceMetadata metadata,
        CancellationToken cancellationToken)
    {
        var imageBase64 = BuildDataUri(filePath);
        var embedding = await _embeddingService.EmbedImageAsync(imageBase64, cancellationToken);

        _context.ClothingReferenceEmbeddings.Add(new ClothingReferenceEmbedding
        {
            Id = Guid.NewGuid(),
            Label = label,
            SourceKey = sourceKey,
            CategoryHint = metadata.CategoryHint,
            SourceDataset = metadata.SourceDataset,
            GenderTag = metadata.GenderTag,
            MasterCategory = metadata.MasterCategory,
            SubCategory = metadata.SubCategory,
            ArticleType = metadata.ArticleType,
            CategoryGroup = metadata.CategoryGroup,
            BaseColour = metadata.BaseColour,
            ColorFamily = metadata.ColorFamily,
            SeasonTag = metadata.SeasonTag,
            UsageTag = metadata.UsageTag,
            DisplayName = metadata.DisplayName,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
    }

    private async Task FlushBatchAsync(int addedCount, CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, _settings.SeedSaveBatchSize);
        if (addedCount != 1 && addedCount % batchSize != 0)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
        _logger.LogInformation("Seeded {Count} clothing reference embeddings so far.", addedCount);
    }

    private static ReferenceMetadata BuildPrimaryReferenceMetadata(PrimaryDatasetRow row)
    {
        var articleType = NormalizeValue(row.ArticleType);
        var subCategory = NormalizeValue(row.SubCategory);
        var masterCategory = NormalizeValue(row.MasterCategory);

        return new ReferenceMetadata
        {
            CategoryHint = articleType ?? subCategory ?? masterCategory,
            SourceDataset = NormalizeValue(row.SourceDataset),
            GenderTag = NormalizeValue(row.GenderTag),
            MasterCategory = masterCategory,
            SubCategory = subCategory,
            ArticleType = articleType,
            CategoryGroup = MapPrimaryCategoryGroup(masterCategory, subCategory, articleType),
            BaseColour = null,
            ColorFamily = null,
            SeasonTag = NormalizeValue(row.SeasonTag),
            UsageTag = NormalizeValue(row.UsageTag),
            DisplayName = NormalizeValue(row.DisplayName)
        };
    }

    private static ReferenceMetadata BuildLegacyReferenceMetadata(LegacyDatasetRow row)
    {
        var articleType = NormalizeValue(row.Label);
        return new ReferenceMetadata
        {
            CategoryHint = articleType,
            SourceDataset = "clothing_dataset",
            ArticleType = articleType,
            CategoryGroup = MapLegacyCategoryGroup(row.Label)
        };
    }

    private static PrimaryDatasetRow? ParsePrimaryDatasetRow(string line)
    {
        var parts = ParseCsvLine(line);
        if (parts.Count < 11)
        {
            return null;
        }

        return new PrimaryDatasetRow(
            parts[0],
            parts[1],
            parts[2],
            parts[3],
            parts[4],
            parts[5],
            parts[6],
            parts[7],
            parts[8],
            parts[9],
            parts[10]);
    }

    private static LegacyDatasetRow? ParseLegacyDatasetRow(string line)
    {
        var parts = ParseCsvLine(line);
        if (parts.Count < 4 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return null;
        }

        return new LegacyDatasetRow(parts[0], parts[2], string.Equals(parts[3], "True", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string BuildDataUri(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var mimeType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };

        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static string ResolveCategoryHint(string folderPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(folderPath, filePath);
        var directoryName = Path.GetDirectoryName(relativePath);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            var normalized = directoryName
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string MapPrimaryCategoryGroup(string? masterCategory, string? subCategory, string? articleType)
    {
        var article = NormalizeValue(articleType) ?? string.Empty;
        var sub = NormalizeValue(subCategory) ?? string.Empty;
        var master = NormalizeValue(masterCategory) ?? string.Empty;

        if (master == "footwear")
        {
            return "shoes";
        }

        if (master == "accessories")
        {
            return "accessories";
        }

        if (ContainsAny(article, "dress", "dresses", "saree"))
        {
            return "dress";
        }

        if (ContainsAny(article, "jumpsuit", "romper"))
        {
            return "jumpsuit";
        }

        if (ContainsAny(article, "blazer", "jacket", "coat", "sweatshirt", "hoodie", "cardigan", "waistcoat"))
        {
            return "outerwear";
        }

        if (sub == "topwear" || ContainsAny(article, "shirt", "shirts", "t-shirt", "tshirts", "tops", "top", "kurta", "kurtas", "tunic", "bra"))
        {
            return "top";
        }

        if (sub == "bottomwear" || ContainsAny(article, "jeans", "trousers", "pants", "shorts", "skirt", "skirts", "leggings", "capris", "tracksuits"))
        {
            return "bottom";
        }

        return "other";
    }

    private static string MapLegacyCategoryGroup(string? label)
    {
        var normalized = NormalizeValue(label) ?? string.Empty;
        if (ContainsAny(normalized, "dress"))
        {
            return "dress";
        }

        if (ContainsAny(normalized, "pants", "shorts", "skirt"))
        {
            return "bottom";
        }

        if (ContainsAny(normalized, "jumpsuit"))
        {
            return "jumpsuit";
        }

        if (ContainsAny(normalized, "outwear", "outerwear", "blazer", "hoodie"))
        {
            return "outerwear";
        }

        if (ContainsAny(normalized, "shoe"))
        {
            return "shoes";
        }

        if (ContainsAny(normalized, "hat"))
        {
            return "accessories";
        }

        return "top";
    }

    private static string? MapColorFamily(string? baseColour)
    {
        var normalized = NormalizeValue(baseColour);
        if (normalized is null)
        {
            return null;
        }

        return normalized switch
        {
            var colour when ContainsAny(colour, "black") => "black",
            var colour when ContainsAny(colour, "white", "off white", "cream") => "white",
            var colour when ContainsAny(colour, "grey", "gray", "silver", "charcoal") => "gray",
            var colour when ContainsAny(colour, "brown", "khaki", "tan", "beige", "taupe", "camel") => "brown",
            var colour when ContainsAny(colour, "blue", "navy", "teal", "turquoise") => "blue",
            var colour when ContainsAny(colour, "green", "olive", "lime") => "green",
            var colour when ContainsAny(colour, "red", "maroon", "burgundy") => "red",
            var colour when ContainsAny(colour, "pink", "magenta", "peach", "coral") => "pink",
            var colour when ContainsAny(colour, "purple", "lavender", "violet") => "purple",
            var colour when ContainsAny(colour, "orange", "rust") => "orange",
            var colour when ContainsAny(colour, "yellow", "mustard", "gold") => "yellow",
            var colour when ContainsAny(colour, "multi") => "multicolor",
            _ => normalized
        };
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ReferenceMetadata
    {
        public string? CategoryHint { get; init; }
        public string? SourceDataset { get; init; }
        public string? GenderTag { get; init; }
        public string? MasterCategory { get; init; }
        public string? SubCategory { get; init; }
        public string? ArticleType { get; init; }
        public string? CategoryGroup { get; init; }
        public string? BaseColour { get; init; }
        public string? ColorFamily { get; init; }
        public string? SeasonTag { get; init; }
        public string? UsageTag { get; init; }
        public string? DisplayName { get; init; }
    }

    private sealed record PrimaryDatasetRow(
        string Id,
        string GenderTag,
        string MasterCategory,
        string SubCategory,
        string ArticleType,
        string BaseColour,
        string SeasonTag,
        string UsageTag,
        string DisplayName,
        string SourceDataset,
        string ImagePath);

    private sealed record LegacyDatasetRow(string ImageId, string Label, bool Kids);
}
