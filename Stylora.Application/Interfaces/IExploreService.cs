using Stylora.Application.DTOs;

namespace Stylora.Application.Interfaces;

public interface IExploreService
{
    Task<ExploreResultDto> SearchAsync(ExploreQueryDto query);
}
