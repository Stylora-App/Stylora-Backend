using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stylora.Application.Models;

namespace Stylora.Infrastructure.Services;

public sealed class GemmaIntentWorkerWarmupHostedService : IHostedService
{
    private readonly OutfitChatModelSettings _settings;
    private readonly GemmaIntentWorkerService _workerService;
    private readonly ILogger<GemmaIntentWorkerWarmupHostedService> _logger;

    public GemmaIntentWorkerWarmupHostedService(
        OutfitChatModelSettings settings,
        GemmaIntentWorkerService workerService,
        ILogger<GemmaIntentWorkerWarmupHostedService> logger)
    {
        _settings = settings;
        _workerService = workerService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.WarmupWorkerOnStartup)
        {
            return;
        }

        try
        {
            await _workerService.WarmupAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemma intent worker warmup failed. Heuristic parsing will remain available.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
