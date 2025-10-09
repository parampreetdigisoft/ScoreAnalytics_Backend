using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.PublicDto;

namespace AssessmentPlatform.IServices
{
    public interface ICityUserService
    {
        Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userID);
        Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID);
        Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto);
    }
}
