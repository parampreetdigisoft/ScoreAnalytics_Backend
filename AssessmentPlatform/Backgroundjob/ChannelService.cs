using System.Threading.Channels;

namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelService
    {
        private readonly Dictionary<string, Channel<string>> _channels = new();

        public Channel<string> GetChannel(string channelName)
        {
            if (!_channels.ContainsKey(channelName))
            {
                _channels[channelName] = Channel.CreateUnbounded<string>();
            }
            return _channels[channelName];
        }

        public async Task SendMessageAsync(string channelName, string message)
        {
            var channel = GetChannel(channelName);
            await channel.Writer.WriteAsync(message);
        }

        public ChannelReader<string> GetReader(string channelName)
        {
            return GetChannel(channelName).Reader;
        }
    }
}
