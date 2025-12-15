using AssessmentPlatform.Models;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.IServices
{
    public interface IAIComputationService
    {
        Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole);
    }
}
