using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;

namespace AssessmentPlatform.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request);
    }
}
