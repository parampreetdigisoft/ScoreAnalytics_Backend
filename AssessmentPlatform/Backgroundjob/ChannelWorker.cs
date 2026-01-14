using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelWorker : BackgroundService
    {
        #region Constructor
      
        private readonly ChannelService _channelService;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Func<Download, Task>> _actionHandlers;
        private readonly Dictionary<int, CancellationTokenSource> _debounceTokens;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMinutes(2);

        public ChannelWorker(ChannelService channelService, IServiceProvider serviceProvider)
        {
            _channelService = channelService;
            _serviceProvider = serviceProvider;
            _debounceTokens = new Dictionary<int, CancellationTokenSource>();

            _actionHandlers = new Dictionary<string, Func<Download, Task>>
            {
                { "InsertAnalyticalLayerResults", InsertAnalyticalLayerResults },
                { "LogException", LogException },
                { "AiResearchByCityId", AiResearchByCityId },
            };
        }
        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queueItem = await _channelService.Read();

                    if (_actionHandlers.TryGetValue(queueItem.Type, out var action))
                    {
                        if (queueItem.Type == "InsertAnalyticalLayerResults")
                        {
                            //Called sp on latest changes
                            Debounce(queueItem.CityID ?? 0, async () => await action(queueItem));
                        }
                        else
                        {
                            await action(queueItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // log exception
                }
            }
        }
        #region Debounce
 
        private void Debounce(int key, Func<Task> action)
        {
            // Cancel previous timer if it exists
            if (_debounceTokens.TryGetValue(key, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _debounceTokens[key] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for inactivity period
                    await Task.Delay(_debounceInterval, cts.Token);

                    // Execute if not cancelled
                    await action();
                }
                catch (TaskCanceledException)
                {
                    // ignored — means another call came in before debounce finished
                }
            });
        }
        #endregion

        #region InsertAnalyticalLayerResults

        private async Task InsertAnalyticalLayerResults(Download channel)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cityIdParam = new SqlParameter("@CityID", channel.CityID ?? 0);
                await dbContext.Database.ExecuteSqlRawAsync("EXEC sp_InsertAnalyticalLayerResults @CityID", cityIdParam);
            }
            catch (Exception ex)
            {
                channel.Level = "Background running";
                channel.Exception = ex.ToString();
                channel.Message = $"Error accour in executing sp_InsertAnalyticalLayerResults for city {channel.CityID}";
                await LogException(channel);
            }
        }
        #endregion
       
        #region LogException

        private async Task LogException(Download channel)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var log = new AppLogs
            {
                Level = channel.Level,
                Message = channel.Message,
                Exception = channel.Exception
            };

            dbContext.AppLogs.Add(log);
            await dbContext.SaveChangesAsync();
        }
        #endregion
        
        #region AiResearchByCityId

        private async Task AiResearchByCityId(Download channel)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var aiService = scope.ServiceProvider.GetRequiredService<IAIAnalyzeService>();
                if(channel.CityID > 0)
                {
                    if (channel.QuestionEnable)
                        await aiService.AnalyzeQuestionsOfCity(channel.CityID.Value);

                    if (channel.PillarEnable)
                        await aiService.AnalyzeCityPillars(channel.CityID.Value);

                    if (channel.CityEnable)
                        await aiService.AnalyzeSingleCity(channel.CityID.Value);
                }

                
            }
            catch (Exception ex)
            {
                channel.Level = "Background running";
                channel.Exception = ex.ToString();
                channel.Message = $"Error accour in executing sp_InsertAnalyticalLayerResults for city {channel.CityID}";
                await LogException(channel);
            }
        }
        #endregion
    }
}
