using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface ICityService
    {
        Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request, UserRole userRole);
        Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCityByUserId(int userId, UserRole userRole);
        Task<ResultResponseDto<City>> GetByIdAsync(int id);
        Task<ResultResponseDto<string>> AddBulkCityAsync(BulkAddCityDto q, string image = "");
        Task<ResultResponseDto<City>> EditCityAsync(int id, AddUpdateCityDto q);
        Task<ResultResponseDto<bool>> DeleteCityAsync(int id);
        Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> EditAssingCity(int id,int userId, int cityId, int AssignedByUserId);
        Task<ResultResponseDto<object>> UnAssignCity(UserCityUnMappingRequestDto requestDto);
        Task<ResultResponseDto<List<UserCityMappingResponseDto>>> GetCityByUserIdForAssessment(int userId);
        Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userID, DateTime updatedA);
        Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID, DateTime updateAt);
        Task<ResultResponseDto<string>> AddUpdateCity(AddUpdateCityDto q);
        Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCityByLocation(GetNearestCityRequestDto r);
    }
}
