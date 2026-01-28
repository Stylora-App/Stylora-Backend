using Microsoft.Extensions.DependencyInjection;
using Stylora.Application.Services;

namespace Stylora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AnalysisService>();
        services.AddScoped<WardrobeService>();
        services.AddScoped<TryOnService>();
        services.AddScoped<OutfitService>();

        return services;
    }
}
