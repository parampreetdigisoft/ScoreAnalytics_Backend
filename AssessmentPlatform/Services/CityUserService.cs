
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.IServices;
using Microsoft.EntityFrameworkCore;

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
        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCities()
        {
            try
            {
               var result = await _context.Cities.Where(c=>c.IsActive && !c.IsDeleted).
                Select(c=> new UserCityMappingResponseDto
                {
                    CityID = c.CityID,
                    State = c.State,
                    CityName = c.CityName,
                    PostalCode = c.PostalCode,
                    Region = c.Region,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted
                }).OrderBy(x => x.CityName).ToListAsync();

                return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new string[] { "get All Cities successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCities", ex);
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

    }
}
