using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IAIComputationService
    {
        Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels();
        Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(int cityID, int userID, UserRole userRole);
        Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCityPillarSummeryRequestDto r, int userID, UserRole userRole);
        Task<IQueryable<AiCitySummeryDto>> GetCityAiSummeryDetails(int userID, UserRole userRole, int? cityID);
        Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto cityDetails);
        Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarReponse cityDetails);
    }
}
