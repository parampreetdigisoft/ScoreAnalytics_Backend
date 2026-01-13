using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.IServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Common.Implementation
{
    public class CommonService : ICommonService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        public CommonService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
        }
        #endregion

        public async Task<List<EvaluationCityProgressResultDto>> GetCitiesProgressAsync(int userId, int role, int year)
        {
            try
            {
                return await _context.CityProgressResults
                 .FromSqlRaw(
                     "EXEC usp_getCitiesProgressByUserId @userID, @role, @year",
                     new SqlParameter("@userID", userId),
                     new SqlParameter("@role", role),
                     new SqlParameter("@year", year)
                 )
                 .AsNoTracking()
                 .ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Executing usp_getCitiesProgressByUserId", ex);
                return new List<EvaluationCityProgressResultDto>();
            }
        }
    }
}
