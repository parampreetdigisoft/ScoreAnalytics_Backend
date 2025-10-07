namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelWorker : BackgroundService
    {
        private readonly ChannelService _channelService;

        public ChannelWorker(ChannelService channelService)
        {
            _channelService = channelService;
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channelsToListen = new[] { "Notify", "Sanction" };

            var tasks = channelsToListen.Select(channel => ProcessChannel(channel, stoppingToken));

            await Task.WhenAll(tasks);
        }

        private async Task ProcessChannel(string channelName, CancellationToken token)
        {
            var reader = _channelService.GetReader(channelName);

            await foreach (var message in reader.ReadAllAsync(token))
            {
                // Perform action per channel
                switch (channelName)
                {
                    case "Notify":
                        Console.WriteLine($"Notification sent: {message}");
                        // Call your notification service here
                        break;

                    case "Sanction":
                        Console.WriteLine($"Sanction performed: {message}");
                        // Call your sanction logic here
                        break;
                }
            }
        }
    }

}
