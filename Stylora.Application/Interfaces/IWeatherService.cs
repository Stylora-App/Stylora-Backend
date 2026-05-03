using Stylora.Application.Models;

namespace Stylora.Application.Interfaces;

public interface IWeatherService
{
    Task<ResolvedWeatherContext?> ResolveAsync(
        OutfitIntentResult intent,
        CancellationToken cancellationToken = default);
}
