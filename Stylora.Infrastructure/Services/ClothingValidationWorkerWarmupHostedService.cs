using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class ClothingValidationWorkerWarmupHostedService : BackgroundService
{
    private readonly ClipImageEmbeddingWorkerService _workerService;
    private readonly ClothingValidationSettings _settings;
    private readonly ILogger<ClothingValidationWorkerWarmupHostedService> _logger;

    public ClothingValidationWorkerWarmupHostedService(
        ClipImageEmbeddingWorkerService workerService,
        ClothingValidationSettings settings,
        ILogger<ClothingValidationWorkerWarmupHostedService> logger)
    {
        _workerService = workerService;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.WarmupWorkerOnStartup)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Starting background warmup for the clothing validation worker.");
            await _workerService.WarmupAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background warmup for the clothing validation worker did not complete.");
        }
    }
}
