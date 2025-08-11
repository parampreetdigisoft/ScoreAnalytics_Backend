using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface ICityService
    {
        Task<PaginationResponse<City>> GetCitiesAsync(PaginationRequest request);
        Task<City> GetByIdAsync(int id);
        Task<City> AddCityAsync(City q);
        Task<City> EditCityAsync(int id, City q);
        Task<bool> DeleteCityAsync(int id);
        Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> EditAssingCity(int id,int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> DeleteAssingCity(int id);
    }
}
