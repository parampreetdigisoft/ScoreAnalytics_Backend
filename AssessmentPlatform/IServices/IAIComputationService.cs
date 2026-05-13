using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Mvc;


namespace AssessmentPlatform.IServices
{
    public interface IAIComputationService
    {
        Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels();
        Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(int cityID, int userID, UserRole userRole,int year=0);
        Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCityPillarSummeryRequestDto r, int userID, UserRole userRole);
        Task<IQueryable<AiCitySummeryDto>> GetCityAiSummeryDetails(int userID, UserRole userRole, int? cityID, int year=0);
        Task<byte[]> GenerateCityDetailsReport(AiCitySummeryDto cityDetails, UserRole userRole, int userID, Common.Interface.DocumentFormat format = Common.Interface.DocumentFormat.Pdf, string reportType = "AI");
        Task<byte[]> GeneratePillarDetailsReport(AiCityPillarReponse cityDetails, UserRole userRole, Common.Interface.DocumentFormat format = Common.Interface.DocumentFormat.Pdf);
        Task<ResultResponseDto<AiCrossCityResponseDto>> GetAICrossCityPillars(AiCityIdsDto ids, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> ChangedAiCityEvaluationStatus(ChangedAiCityEvaluationStatusDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> AddComment(AddCommentDto aiCityIdsDto, int userID, UserRole userRole);
        Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto aiCityIdsDto, int userID, UserRole userRole);
        Task<AiCitySummeryDto> GetCityAiSummeryDetail(int userID, UserRole userRole, int? cityID, int year, string reportType = "AI");
        Task<List<AiCitySummeryDto>> GetAllCityAiSummeryDetail(int userID, UserRole userRole, int year);
        Task<byte[]> GenerateAllCityDetailsReport(List<AiCitySummeryDto> cityDetails, UserRole userRole, int userID, int year, Common.Interface.DocumentFormat format = Common.Interface.DocumentFormat.Pdf);
        public Task<ResultResponseDto<Dictionary<int, List<AiCityPillarReponse>>>> GetAllCitiesAIPillars(int userID, UserRole userRole, int currentYear = 0);
        Task<ResultResponseDto<string>> AITransferAssessment(AITransferAssessmentRequestDto r, int userID, UserRole userRole);
        Task<ResultResponseDto<string>> ReCalculateKpis(int userID, UserRole userRole);

        Task<ResultResponseDto<string>> UploadAiDocuments(UploadAiDocumentRequest r, int userID, UserRole userRole);
        Task<PaginationResponse<GetCityDocumentResponseDto>> GetAICityDocuments(AiCityDocumentRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<List<GetCityPillarDocumentResponseDto>>> GetAICityPillarDocuments(AiCityPillarDocumentRequestDto request, int userID, UserRole userRole);
        Task<ResultResponseDto<string>> DeleteDocument(DeleteCityDocumentRequestDto request, int userID, UserRole userRole);
        Task<FileResult> DownloadDocument(int cityDocumentID, int userID, UserRole userRole);

    }
}
