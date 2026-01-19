using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelWorker : BackgroundService
    {
        #region Constructor
      
        private readonly ChannelService _channelService;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Func<Download, Task>> _actionHandlers;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _debounceTokens = new();
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _cityLocks = new();
        private readonly TimeSpan _debounceInterval = TimeSpan.FromMinutes(2);

        public ChannelWorker(ChannelService channelService, IServiceProvider serviceProvider)
        {
            _channelService = channelService;
            _serviceProvider = serviceProvider;

            _actionHandlers = new Dictionary<string, Func<Download, Task>>
            {
                { "InsertAnalyticalLayerResults", InsertAnalyticalLayerResults },
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
                            await DebounceAsync(queueItem.CityID ?? 0,
                                () => action(queueItem));
                        }
                        else
                        {
                            await action(queueItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var _appLogger = scope.ServiceProvider.GetRequiredService<IAppLogger>();
                    await _appLogger.LogAsync("ChannelWorker", ex);
                }
            }
        }

        #region Debounce

        private async Task DebounceAsync(int cityId, Func<Task> action)
        {
            var cts = _debounceTokens.AddOrUpdate(cityId, _ => new CancellationTokenSource(), (_, existing) =>
            {
                existing.Cancel();
                existing.Dispose();
                return new CancellationTokenSource();
            });

            try
            {
                await Task.Delay(_debounceInterval, cts.Token);

                var semaphore = _cityLocks.GetOrAdd(cityId, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync(cts.Token);

                try
                {
                    await action();
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (TaskCanceledException)
            {
                // debounce cancelled – expected
            }
            finally
            {
                _debounceTokens.TryRemove(cityId, out _);
            }
        }

        #endregion

        #region InsertAnalyticalLayerResults

        private async Task InsertAnalyticalLayerResults(Download channel)
        {
            using var scope = _serviceProvider.CreateScope();
            var _appLogger = scope.ServiceProvider.GetRequiredService<IAppLogger>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cityIdParam = new SqlParameter("@CityID", channel.CityID ?? 0);

            try
            {
                await ExecuteWithRetry(
                    async () =>
                    {
                        await dbContext.Database.ExecuteSqlRawAsync("EXEC sp_InsertAnalyticalLayerResults @CityID", cityIdParam);
                    },
                    onFinalFailure: ex =>
                    {
                       
                        _appLogger.LogAsync("ChannelWorker", ex);
                    });

                await ExecuteWithRetry(
                    async () =>
                    {
                        await dbContext.Database.ExecuteSqlRawAsync("EXEC sp_AiInsertAnalyticalLayerResults @CityID",cityIdParam);
                    },
                    onFinalFailure: ex =>
                    {
                         _appLogger.LogAsync("sp_AiInsertAnalyticalLayerResults", ex);
                    });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("InsertAnalyticalLayerResults", ex);
            }
        }
        public async Task ExecuteWithRetry(Func<Task> action,int maxRetry = 3, Action<Exception>? onFinalFailure = null)
        {
            int retry = 0;

            while (true)
            {
                try
                {
                    await action();
                    return;
                }
                catch (SqlException ex) when (ex.Number == 1205)
                {
                    retry++;

                    if (retry > maxRetry)
                    {
                        onFinalFailure?.Invoke(ex);
                        throw; // let caller catch it
                    }

                    await Task.Delay(500 * retry); // exponential backoff
                }
            }
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
                using var scope = _serviceProvider.CreateScope();
                var _appLogger = scope.ServiceProvider.GetRequiredService<IAppLogger>();
                await _appLogger.LogAsync("AiResearchByCityId", ex);
            }
        }
        #endregion
    }
}
