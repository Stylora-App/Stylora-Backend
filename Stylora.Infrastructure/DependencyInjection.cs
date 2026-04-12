using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stylora.Application.Interfaces;
using Stylora.Infrastructure.Data;
using Stylora.Infrastructure.Repositories;
using Stylora.Infrastructure.Services;

namespace Stylora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        string geminiApiKey,
        string connectionString,
        string rapidApiKey)
    {
        // Configure PostgreSQL with EF Core
        services.AddDbContext<StyloraDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(StyloraDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            }));

        // Register Gemini AI service
        services.AddSingleton<IGeminiService>(sp => new GeminiService(geminiApiKey));
        
        // Register ASOS shopping service
        services.AddSingleton<IAsosService>(sp => new AsosService(rapidApiKey));
        
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWardrobeRepository, WardrobeRepository>();
        services.AddScoped<ISeasonAnalysisRepository, SeasonAnalysisRepository>();
        services.AddScoped<ITryOnRepository, TryOnRepository>();
        
        // Register services
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
