using Microsoft.Extensions.DependencyInjection;
using Stylora.Application.Interfaces;
using Stylora.Infrastructure.Repositories;
using Stylora.Infrastructure.Services;

namespace Stylora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string geminiApiKey)
    {
        // Register Gemini Service
        services.AddSingleton<IGeminiService>(sp => new GeminiService(geminiApiKey));
        
        // Register Repository (in-memory for now, can be replaced with database later)
        services.AddSingleton<IWardrobeRepository, InMemoryWardrobeRepository>();

        return services;
    }
}
