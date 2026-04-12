using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IAsosService
{
    Task<List<ShoppingProductDto>> SearchProductsAsync(string query, int limit, int offset);
}
