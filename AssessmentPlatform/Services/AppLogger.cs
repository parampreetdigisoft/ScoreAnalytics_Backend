using AssessmentPlatform.Backgroundjob.logging;
using AssessmentPlatform.IServices;
namespace AssessmentPlatform.Services
{
    public class AppLogger : IAppLogger
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LogChannelService _logChannel;
        private readonly ILogger<AppLogger> _fallbackLogger;

        public AppLogger(IHttpContextAccessor httpContextAccessor, LogChannelService logChannel, ILogger<AppLogger> fallbackLogger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logChannel = logChannel;
            _fallbackLogger = fallbackLogger;
        }

        public Task LogAsync(string message, Exception? ex = null)
        {
            var entry = new LogEntry
            {
                Level = GetCurrentUrl() ?? "Unknown",
                Message = message,
                Exception = ex?.ToString() ?? string.Empty,
                Timestamp = DateTime.UtcNow
            };
            // Non-blocking write
            if (!_logChannel.TryWrite(entry))
            {
                // Channel full - use fallback
                _fallbackLogger.LogWarning("Log channel full, using fallback for: {Message}", message);
            }
            return Task.CompletedTask;
        }

        public void LogWarning(string message) => LogAsync(message, null);
        public void LogInfo(string message) => LogAsync(message, null);

        private string? GetCurrentUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            return request == null ? null : $"{request.Path}{request.QueryString}";
        }
    }
}
