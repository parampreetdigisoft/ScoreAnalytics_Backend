using AssessmentPlatform.IServices;

namespace AssessmentPlatform.Backgroundjob
{
    public class AiJobService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public AiJobService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run both schedules in parallel
            //_ = RunEvery2Hours(stoppingToken);
            //_ = RunEveryMonth(stoppingToken);

            await Task.CompletedTask;
        }

        private async Task RunEvery2Hours(CancellationToken token)
        {
            var timer = new PeriodicTimer(TimeSpan.FromHours(2));

            do
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAIAnalyzeService>();

                    //await aiService.RunEvery2HoursJob();
                }
                catch (Exception ex)
                {
                    // log error
                    Console.WriteLine(ex.Message);
                }
            } while (await timer.WaitForNextTickAsync(token));
        }

        private async Task RunEveryMonth(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // Run at 1st day of every month at 01:00 AM UTC
                var nextRun = new DateTime(now.Year, now.Month, 1, 1, 0, 0, DateTimeKind.Utc)
                                .AddMonths(1);

                var delay = nextRun - now;

                await Task.Delay(delay, token); // wait until next month

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAIAnalyzeService>();

                   // await aiService.RunMonthlyJob();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }

}
