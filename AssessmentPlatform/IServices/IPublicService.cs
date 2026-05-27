using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.chatDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.PublicDto;

namespace AssessmentPlatform.IServices
{
    public interface IPublicService
    {
        Task<ResultResponseDto<List<PartnerCityResponseDto>>> GetAllCities();
        Task<ResultResponseDto<PartnerCityFilterResponse>> GetPartnerCitiesFilterRecord();
        Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync();
        Task<PaginationResponse<PartnerCityResponseDto>> GetPartnerCities(PartnerCityRequestDto r);
        Task<CountryCityResponse> GetCountriesAndCities_WithStaleSupport();
        Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedCities();
        Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues(int cityCount);
        Task<ResultResponseDto<PillarLiveSignalsResult>> GetPillarLiveSignals();
        Task<bool> RefreshEmergingTrendsCacheAsync(int cityCount, CancellationToken cancellationToken = default);
    }
}
