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

        var (datasetAvailable, datasetSeeded) = await SeedDatasetAsync(root, existingKeys, cancellationToken);
        seededCount += datasetSeeded;

        if (!datasetAvailable)
        {
            seededCount += await SeedFolderAsync(root, "clothing", ClothingReferenceLabel.Clothing, existingKeys, cancellationToken);
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

    private async Task<(bool DatasetAvailable, int AddedCount)> SeedDatasetAsync(string root, HashSet<string> existingKeys, CancellationToken cancellationToken)
    {
        var datasetRoot = Path.Combine(root, _settings.DatasetDirectoryName);
        var metadataPath = Path.Combine(datasetRoot, _settings.DatasetMetadataFileName);
        var imageDirectory = Path.Combine(datasetRoot, _settings.DatasetImageDirectoryName);

        if (!Directory.Exists(datasetRoot) || !File.Exists(metadataPath) || !Directory.Exists(imageDirectory))
        {
            return (false, 0);
        }

        var excludedLabels = _settings.DatasetExcludedLabels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var row in File.ReadLines(metadataPath).Skip(1).Select(ParseDatasetRow))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row is null || excludedLabels.Contains(row.Label))
            {
                continue;
            }

            var filePath = Path.Combine(imageDirectory, $"{row.ImageId}.jpg");
            if (!File.Exists(filePath))
            {
                continue;
            }

            var sourceKey = $"{_settings.DatasetDirectoryName}/{_settings.DatasetImageDirectoryName}/{row.ImageId}.jpg";
            if (!existingKeys.Add(sourceKey))
            {
                continue;
            }

            if (added == 0)
            {
                _logger.LogInformation("Embedding first dataset clothing reference from {SourceKey}", sourceKey);
            }

            await AddEmbeddingAsync(
                filePath,
                sourceKey,
                ClothingReferenceLabel.Clothing,
                row.Label,
                cancellationToken);

            added++;
            await FlushBatchAsync(added, cancellationToken);
        }

        if (added > 0)
        {
            _logger.LogInformation("Seeded {Count} clothing embeddings from dataset {DatasetRoot}", added, datasetRoot);
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

            await AddEmbeddingAsync(
                filePath,
                sourceKey,
                label,
                ResolveCategoryHint(folderPath, filePath),
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
        string? categoryHint,
        CancellationToken cancellationToken)
    {
        var imageBase64 = BuildDataUri(filePath);
        var embedding = await _embeddingService.EmbedImageAsync(imageBase64, cancellationToken);

                _context.ClothingReferenceEmbeddings.Add(new ClothingReferenceEmbedding
                {
                    Id = Guid.NewGuid(),
                    Label = label,
                    SourceKey = sourceKey,
                    CategoryHint = categoryHint,
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

    private static ClothingDatasetRow? ParseDatasetRow(string line)
    {
        var parts = line.Split(',', 4, StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return null;
        }

        return new ClothingDatasetRow(parts[0], parts[2], string.Equals(parts[3], "True", StringComparison.OrdinalIgnoreCase));
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

    private sealed record ClothingDatasetRow(string ImageId, string Label, bool Kids);
}
