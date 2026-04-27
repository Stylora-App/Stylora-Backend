using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Domain.Enums;
using Stylora.Infrastructure.Data;

namespace Stylora.Infrastructure.Repositories;

public class ClothingReferenceEmbeddingRepository : IClothingReferenceEmbeddingRepository
{
    private readonly StyloraDbContext _context;

    public ClothingReferenceEmbeddingRepository(StyloraDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsAsync(float[] embedding, int count, CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(embedding);
        var matches = new List<ClothingReferenceMatch>(count);
        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Label",
                       "SourceKey",
                       "CategoryHint",
                       "GenderTag",
                       "MasterCategory",
                       "SubCategory",
                       "ArticleType",
                       "CategoryGroup",
                       "BaseColour",
                       "ColorFamily",
                       "SeasonTag",
                       "UsageTag",
                       "DisplayName",
                       "Embedding" <=> @query_embedding AS "Distance"
                FROM "ClothingReferenceEmbeddings"
                WHERE "IsActive" = TRUE
                ORDER BY "Embedding" <=> @query_embedding
                LIMIT @count
                """;

            if (command is not NpgsqlCommand npgsqlCommand)
            {
                throw new InvalidOperationException("The clothing validation repository requires an Npgsql database connection.");
            }

            npgsqlCommand.Parameters.AddWithValue("query_embedding", queryVector);
            npgsqlCommand.Parameters.AddWithValue("count", count);

            await using var reader = await npgsqlCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(MapMatch(reader));
            }

            return matches;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsByScanAsync(float[] embedding, int count, CancellationToken cancellationToken = default)
    {
        var references = await _context.ClothingReferenceEmbeddings
            .AsNoTracking()
            .Where(reference => reference.IsActive)
            .Select(reference => new
            {
                reference.Label,
                reference.SourceKey,
                reference.CategoryHint,
                reference.GenderTag,
                reference.MasterCategory,
                reference.SubCategory,
                reference.ArticleType,
                reference.CategoryGroup,
                reference.BaseColour,
                reference.ColorFamily,
                reference.SeasonTag,
                reference.UsageTag,
                reference.DisplayName,
                reference.Embedding
            })
            .ToListAsync(cancellationToken);

        return references
            .Where(reference => reference.Embedding is { Length: > 0 })
            .Select(reference => new ClothingReferenceMatch(
                reference.Label,
                reference.SourceKey,
                reference.CategoryHint,
                reference.GenderTag,
                reference.MasterCategory,
                reference.SubCategory,
                reference.ArticleType,
                reference.CategoryGroup,
                reference.BaseColour,
                reference.ColorFamily,
                reference.SeasonTag,
                reference.UsageTag,
                reference.DisplayName,
                CosineDistance(embedding, reference.Embedding)))
            .OrderBy(match => match.Distance)
            .Take(count)
            .ToList();
    }

    private static ClothingReferenceMatch MapMatch(NpgsqlDataReader reader)
    {
        return new ClothingReferenceMatch(
            Enum.Parse<ClothingReferenceLabel>(reader.GetString(0), ignoreCase: true),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.GetDouble(13));
    }

    private static double CosineDistance(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return 1d;
        }

        double dot = 0d;
        double leftMagnitude = 0d;
        double rightMagnitude = 0d;
        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= 0d || rightMagnitude <= 0d)
        {
            return 1d;
        }

        var similarity = dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
        return 1d - similarity;
    }
}
