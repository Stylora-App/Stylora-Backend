using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface ITryOnService
{
    Task<TryOnResponse> GenerateTryOnAsync(TryOnRequest request, Guid userId);
    Task<LastTryOnPhotoDto?> GetLastPersonPhotoAsync(Guid userId);
}
