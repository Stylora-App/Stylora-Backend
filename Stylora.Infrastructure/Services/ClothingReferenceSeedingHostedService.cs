using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Stylora.Infrastructure.Services;

public sealed class ClothingReferenceSeedingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClothingReferenceSeedingHostedService> _logger;

    public ClothingReferenceSeedingHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ClothingReferenceSeedingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var seedService = scope.ServiceProvider.GetRequiredService<ClothingReferenceSeedService>();
            await seedService.SeedAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background clothing reference seeding failed.");
        }
    }
}
