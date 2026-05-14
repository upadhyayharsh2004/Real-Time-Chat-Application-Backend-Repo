using ConnectHub.Media.Services.Interfaces;

namespace ConnectHub.Media.Services.Implementations;

/// <summary>
/// MediaCleanupService — IHostedService that cleans up expired files daily.
/// As per class diagram: CleanupExpiredFiles() is triggered by IHostedService.
/// Runs once at startup then every 24 hours.
/// Uses IServiceScopeFactory for scoped IMediaService access.
/// </summary>
public class MediaCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public MediaCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[MediaCleanup] Service started. Running every {Hours}h.", 24);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

                _logger.LogInformation("[MediaCleanup] Running expired file cleanup...");
                await mediaService.CleanupExpiredFiles();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaCleanup] Error during cleanup.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
