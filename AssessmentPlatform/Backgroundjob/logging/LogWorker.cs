using AssessmentPlatform.Data;
using AssessmentPlatform.Models;
namespace AssessmentPlatform.Backgroundjob.logging
{
    public class LogWorker : BackgroundService
    {
        private readonly LogChannelService _logChannel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LogWorker> _logger; 

        public LogWorker(LogChannelService logChannel,IServiceProvider serviceProvider, ILogger<LogWorker> logger)
        {
            _logChannel = logChannel;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batchSize = 50;
            var batchTimeout = TimeSpan.FromSeconds(5);
            var batch = new List<LogEntry>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(batchTimeout);

                    try
                    {
                        while (batch.Count < batchSize)
                        {
                            var entry = await _logChannel.ReadAsync(cts.Token);
                            batch.Add(entry);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout reached or service stopping
                    }

                    if (batch.Count > 0)
                    {
                        await WriteBatchToDatabase(batch);
                        batch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    // CRITICAL: Never throw exceptions from log worker
                    // Use fallback logging (file, console, etc.)
                    _logger.LogError(ex, "LogWorker failed to write batch");
                    batch.Clear(); // Prevent infinite retry
                    await Task.Delay(1000, stoppingToken); // Backoff
                }
            }
        }

        private async Task WriteBatchToDatabase(List<LogEntry> batch)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var logs = batch.Select(entry => new AppLogs
            {
                Level = entry.Level,
                Message = entry.Message,
                Exception = entry.Exception,
                CreatedAt = entry.Timestamp
            }).ToList();

            dbContext.AppLogs.AddRange(logs);

            // Use a shorter timeout for logging to prevent blocking
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await dbContext.SaveChangesAsync(cts.Token);
        }
    }
}
