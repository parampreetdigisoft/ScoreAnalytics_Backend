using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi();
    }
}
