using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IAssessmentResponseService
    {
        Task<List<AssessmentResponse>> GetAllAsync();
        Task<AssessmentResponse> GetByIdAsync(int id);
        Task<AssessmentResponse> AddAsync(AssessmentResponse response);
        Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response);
        Task<bool> DeleteAsync(int id);
        Task<ResultResponseDto<string>> SaveAssessment(AddAssessmentDto request);
        Task<PaginationResponse<GetAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request);
        Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestoinRequestDto request);
        Task<ResultResponseDto<string>> ImportAssessmentAsync(IFormFile file,int userID);
        Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(int userID,int cityID);
        Task<ResultResponseDto<GetAssessmentHistoryDto>> GetAssessmentProgressHistory(int assessmentID);
        Task<List<CityPillarUserHistoryReponseDto>> GetCityPillarHistory(GetCityPillarHistoryRequestDto r);
    }
} 