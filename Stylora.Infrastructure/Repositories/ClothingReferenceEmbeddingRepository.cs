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
                SELECT "Label", "SourceKey", "CategoryHint", "Embedding" <=> @query_embedding AS "Distance"
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
                matches.Add(new ClothingReferenceMatch(
                    Enum.Parse<ClothingReferenceLabel>(reader.GetString(0), ignoreCase: true),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetDouble(3)));
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
}
