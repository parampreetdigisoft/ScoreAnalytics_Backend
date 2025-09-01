using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using System;

namespace AssessmentPlatform.Services
{
    public class AppLogger : IAppLogger
    {
        private readonly ApplicationDbContext _db;

        public AppLogger(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(string level, string message, Exception ex = null)
        {
            var log = new AppLogs
            {
                Level = level,
                Message = message,
                Exception = ex?.ToString() ?? ""
            };

            _db.AppLogs.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
