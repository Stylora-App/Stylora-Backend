using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stylora.Application.Interfaces;
using Stylora.Application.Models;
using Stylora.Application.Services;
using Stylora.Infrastructure.Data;
using Stylora.Infrastructure.Generated.Clip;
using Stylora.Infrastructure.Generated.Gemma;
using Stylora.Infrastructure.Http;
using Stylora.Infrastructure.Repositories;
using Stylora.Infrastructure.Services;
using Pgvector.EntityFrameworkCore;

namespace Stylora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string geminiApiKey,
        string connectionString,
        string rapidApiKey,
        ClothingValidationSettings clothingValidationSettings,
        OutfitChatModelSettings outfitChatModelSettings,
        WeatherApiSettings weatherApiSettings,
        string jwtSecret)
    {
        // Configure PostgreSQL with EF Core
        services.AddDbContext<StyloraDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
                npgsqlOptions.MigrationsAssembly(typeof(StyloraDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            }));

        // AI worker HTTP clients
        services.AddTransient<Http11ConnectionCloseHandler>();

        services.AddHttpClient("clip-worker", client =>
        {
            client.BaseAddress = new Uri(clothingValidationSettings.WorkerBaseUrl);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.DefaultRequestHeaders.ExpectContinue = false;
        }).AddHttpMessageHandler<Http11ConnectionCloseHandler>();

        services.AddSingleton<IClipWorkerClient>(sp =>
            new ClipWorkerClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("clip-worker")));

        services.AddHttpClient("gemma-worker", client =>
        {
            client.BaseAddress = new Uri(outfitChatModelSettings.WorkerBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(outfitChatModelSettings.WorkerRequestTimeoutSeconds);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.DefaultRequestHeaders.ExpectContinue = false;
        }).AddHttpMessageHandler<Http11ConnectionCloseHandler>();

        services.AddSingleton<IGemmaWorkerClient>(sp =>
            new GemmaWorkerClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("gemma-worker")));

        // Register Gemini AI service
        services.AddSingleton<IGeminiService>(sp => new GeminiService(geminiApiKey));
        services.AddSingleton(clothingValidationSettings);
        services.AddSingleton(outfitChatModelSettings);
        services.AddSingleton(weatherApiSettings);
        services.AddSingleton<ClipImageEmbeddingWorkerService>();
        services.AddSingleton<IImageEmbeddingService>(sp => sp.GetRequiredService<ClipImageEmbeddingWorkerService>());
        services.AddSingleton<GemmaIntentWorkerService>();
        services.AddSingleton<IOutfitIntentParser>(sp => sp.GetRequiredService<GemmaIntentWorkerService>());
        services.AddSingleton<IWeatherService, OpenWeatherService>();
        services.AddScoped<IClothingValidationService, ClothingValidationService>();
        services.AddScoped<ClothingReferenceSeedService>();
        services.AddHostedService<ClothingValidationWorkerWarmupHostedService>();
        services.AddHostedService<ClothingReferenceSeedingHostedService>();
        services.AddHostedService<GemmaIntentWorkerWarmupHostedService>();

        // Register ASOS shopping service
        services.AddSingleton<IAsosService>(sp => new AsosService(rapidApiKey));

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWardrobeRepository, WardrobeRepository>();
        services.AddScoped<IClothingReferenceEmbeddingRepository, ClothingReferenceEmbeddingRepository>();
        services.AddScoped<ISeasonAnalysisRepository, SeasonAnalysisRepository>();
        services.AddScoped<ITryOnRepository, TryOnRepository>();

        // Register services
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton(new JwtService(jwtSecret));

        return services;
    }
}
