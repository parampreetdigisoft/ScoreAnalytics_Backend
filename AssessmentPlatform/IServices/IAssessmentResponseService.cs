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
        Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestoin(GetAssessmentQuestoinRequestDto request);
    }
} 