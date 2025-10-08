using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.PublicDto;

namespace AssessmentPlatform.IServices
{
    public interface IPublicService
    {
        Task<ResultResponseDto<PartnerCityFilterResponse>> GetPartnerCitiesFilterRecord();
        Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync();
        Task<PaginationResponse<PartnerCityResponseDto>> GetPartnerCities(PartnerCityRequestDto r);
    }
}
