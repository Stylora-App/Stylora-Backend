using Stylora.Application.Models;

namespace Stylora.Application.Interfaces;

public interface IClothingReferenceEmbeddingRepository
{
    Task<IReadOnlyList<ClothingReferenceMatch>> GetNearestNeighborsAsync(float[] embedding, int count, CancellationToken cancellationToken = default);
}
