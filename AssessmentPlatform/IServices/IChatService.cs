

using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Models;
using AssessmentPlatform.Dtos.chatDto;


namespace AssessmentPlatform.IServices
{
    public interface IChatService
    {
        Task<ResultResponseDto<List<AIAssistantFAQDto>>> GetAssistantFAQDs(int userId, UserRole userRole);
        Task<ResultResponseDto<ChatResponseDto>> AskAboutCity(CityChatRequestDto request);
        Task<ResultResponseDto<ChatResponseDto>> AskAboutGlobal(ChatGlobalAskQuestionRequestDto request);
    }
}
