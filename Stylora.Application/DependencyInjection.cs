using Microsoft.Extensions.DependencyInjection;
using Stylora.Application.Interfaces;
using Stylora.Application.Services;

namespace Stylora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IWardrobeService, WardrobeService>();
        services.AddScoped<ITryOnService, TryOnService>();
        services.AddScoped<IExploreService, ExploreService>();

        return services;
    }
}
