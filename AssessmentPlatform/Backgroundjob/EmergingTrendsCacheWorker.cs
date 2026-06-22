using AssessmentPlatform.IServices;
using Microsoft.Extensions.Configuration;

namespace AssessmentPlatform.Backgroundjob
{
    /// <summary>
    /// Refreshes emerging trends in memory on a schedule. Retries every 10s until success (no cache on failure).
    /// </summary>
public class EmergingTrendsCacheWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmergingTrendsCacheWorker> _logger;

        public EmergingTrendsCacheWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<EmergingTrendsCacheWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cityCount = _configuration.GetValue("EmergingTrendsCache:CityCount", 8);
            var refreshInterval = TimeSpan.FromMinutes(
                _configuration.GetValue("EmergingTrendsCache:RefreshIntervalMinutes", 10));
            var retryDelay = TimeSpan.FromSeconds(
                _configuration.GetValue("EmergingTrendsCache:RetryDelaySeconds", 10));

            await RefreshUntilCachedAsync(cityCount, retryDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await TryRefreshAsync(cityCount, stoppingToken);
            }
        }

        private async Task RefreshUntilCachedAsync(
            int cityCount,
            TimeSpan retryDelay,
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await TryRefreshAsync(cityCount, stoppingToken))
                {
                    return;
                }

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<bool> TryRefreshAsync(int cityCount, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();

                return await publicService.RefreshEmergingTrendsCacheAsync(
                    cityCount,
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Emerging trends cache refresh failed (cityCount={cityCount})",
                    cityCount);
                return false;
            }
        }
    }
}
