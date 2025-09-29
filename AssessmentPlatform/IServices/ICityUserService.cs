using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CityDto;

namespace AssessmentPlatform.IServices
{
    public interface ICityUserService
    {
        Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCities();
    }
}
