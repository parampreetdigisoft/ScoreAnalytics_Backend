using System.Threading.Channels;

namespace AssessmentPlatform.Backgroundjob.logging
{
    public class LogChannelService
    {
        private readonly Channel<LogEntry> _channel;

        public LogChannelService()
        {
            var options = new BoundedChannelOptions(10000) // Limit to prevent memory overflow
            {
                FullMode = BoundedChannelFullMode.DropOldest, // Drop old logs if full
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<LogEntry>(options);
        }

        public bool TryWrite(LogEntry entry)
        {
            // Non-blocking write - returns false if channel is full
            return _channel.Writer.TryWrite(entry);
        }

        public ValueTask<LogEntry> ReadAsync(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAsync(ct);
        }
    }

    public class LogEntry
    {
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
