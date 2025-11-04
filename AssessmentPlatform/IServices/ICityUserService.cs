using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.PublicDto;

namespace AssessmentPlatform.IServices
{
    public interface ICityUserService
    {
        Task<ResultResponseDto<List<PartnerCityResponseDto>>> GetCityUserCities(int userID);
        Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userID);
        Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID);
        Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto);
        Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request);
        Task<ResultResponseDto<CityDetailsDto>> GetCityDetails(UserCityRequstDto userCityRequstDto);
        Task<ResultResponseDto<List<CityPillarQuestionDetailsDto>>> GetCityPillarDetails(UserCityGetPillarInfoRequstDto userCityRequstDto);
        Task<ResultResponseDto<string>> AddCityUserKpisCityAndPillar(AddCityUserKpisCityAndPillar payload,int userID, string tierName);
    }
}
