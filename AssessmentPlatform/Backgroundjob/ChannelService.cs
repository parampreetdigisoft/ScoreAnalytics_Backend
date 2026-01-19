using System.Threading.Channels;

namespace AssessmentPlatform.Backgroundjob
{
    public class ChannelService
    {
        private readonly Channel<Download> _channel;

        public ChannelService()
        {
            var options = new BoundedChannelOptions(10000) // Limit to prevent memory overflow
            {
                FullMode = BoundedChannelFullMode.DropOldest, // Drop old logs if full
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<Download>(options);
        }
        public bool Write(Download entry)
        {
            // Non-blocking write - returns false if channel is full
            return _channel.Writer.TryWrite(entry);
        }

        public async Task<Download> Read(CancellationToken ct = default)
        {
            return await _channel.Reader.ReadAsync(ct);
        }
    }
}
