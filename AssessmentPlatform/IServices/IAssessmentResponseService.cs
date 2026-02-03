using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.dashboard;
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
        Task<PaginationResponse<GetCityAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request, UserRole role);
        Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestoinRequestDto request);
        Task<ResultResponseDto<string>> ImportAssessmentAsync(IFormFile file,int userID);
        Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto);
        Task<ResultResponseDto<GetAssessmentHistoryDto>> GetAssessmentProgressHistory(int assessmentID);
        Task<ResultResponseDto<string>> ChangeAssessmentStatus(ChangeAssessmentStatusRequestDto r);
        Task<ResultResponseDto<string>> TransferAssessment(TransferAssessmentRequestDto r);
        Task<ResultResponseDto<AiCityPillarDashboardResponseDto>> GetCityPillarHistory(UserCityDashBoardRequstDto userCityRequstDto,int userID, UserRole userRole);
    }
} 