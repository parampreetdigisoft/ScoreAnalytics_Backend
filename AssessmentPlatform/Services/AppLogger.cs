using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using System;

namespace AssessmentPlatform.Services
{
    public class AppLogger : IAppLogger
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AppLogger(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string message, Exception ex = null)
        {
            var log = new AppLogs
            {
                Level = GetCurrentUrl() ?? "_",
                Message = message,
                Exception = ex?.ToString() ?? ""
            };

            _db.AppLogs.Add(log);
            await _db.SaveChangesAsync();
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
