using System.Threading.Channels;

namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelService
    {
        public ChannelService()
        {
            var options = new UnboundedChannelOptions { SingleReader = false, SingleWriter = false };
            UnboundedChannel = Channel.CreateUnbounded<Download>(options);
        }

        private Channel<Download> UnboundedChannel { get; }


        public void Write(Download shipmentInboundV2Channel)
        {
            UnboundedChannel.Writer.TryWrite(shipmentInboundV2Channel);
        }

        public async Task<Download> Read()
        {
            return await UnboundedChannel.Reader.ReadAsync();
        }
    }
}
