using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.IServices;
namespace AssessmentPlatform.Services
{
    public class AppLogger : IAppLogger
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Download _download;
        public AppLogger(IHttpContextAccessor httpContextAccessor, Download download)
        {
            _download = download;
            _httpContextAccessor = httpContextAccessor;
        }

        public  Task LogAsync(string message, Exception ex = null)
        {
            return _download.LogException(GetCurrentUrl() ?? "_", message, ex?.ToString() ?? "");
        }

        public string? GetCurrentUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;

            if (request == null)
                return null;

            // Full URL with query string
            return $"{request.Path}{request.QueryString}";
        }
    }
}
