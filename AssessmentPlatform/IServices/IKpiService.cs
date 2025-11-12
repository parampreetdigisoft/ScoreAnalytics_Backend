using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi();
        Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, UserRole role);
    }
}
