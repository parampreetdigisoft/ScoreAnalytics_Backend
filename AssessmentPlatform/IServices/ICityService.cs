using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface ICityService
    {
        Task<PaginationResponse<City>> GetCitiesAsync(PaginationRequest request);
        Task<ResultResponseDto<List<City>>> getAllCityByUserId(int userId);
        Task<ResultResponseDto<City>> GetByIdAsync(int id);
        Task<ResultResponseDto<City>> AddCityAsync(AddUpdateCityDto q);
        Task<ResultResponseDto<City>> EditCityAsync(int id, AddUpdateCityDto q);
        Task<ResultResponseDto<bool>> DeleteCityAsync(int id);
        Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> EditAssingCity(int id,int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> DeleteAssingCity(int id);
    }
}
