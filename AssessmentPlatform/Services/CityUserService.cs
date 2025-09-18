
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Common.Models;

namespace AssessmentPlatform.Services
{
    public class CityUserService : ICityUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public CityUserService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }
        public async Task<ResultResponseDto<string>> CityUserAsync(string str)
        {
            try
            {
               
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddCityAsync", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
            return ResultResponseDto<string>.Success();
        }

    }
}
