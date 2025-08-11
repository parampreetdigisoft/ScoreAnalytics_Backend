using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssessmentPlatform.IServices
{
    public interface IQuestionService
    {
        Task<List<Pillar>> GetPillarsAsync();
        Task<PaginationResponse<GetQuestionRespones>> GetQuestionsAsync(GetQuestionRequestDto requestDto);
        Task<Question> AddQuestionAsync(Question q);
        Task<Question> EditQuestionAsync(int id, Question q);
        Task<bool> DeleteQuestionAsync(int id);
    }
} 