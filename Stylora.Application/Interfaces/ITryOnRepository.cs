using Stylora.Domain.Entities;

namespace Stylora.Application.Interfaces;

public interface ITryOnRepository
{
    Task<TryOnSession?> GetByIdAsync(Guid id);
    Task<IEnumerable<TryOnSession>> GetByUserIdAsync(Guid userId);
    Task<TryOnSession> CreateAsync(TryOnSession session);
    Task<TryOnSession> UpdateAsync(TryOnSession session);
    Task<bool> DeleteAsync(Guid id);
}
