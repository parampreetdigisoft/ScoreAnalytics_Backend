using System.Collections.Generic;
using System.Threading.Tasks;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IQuestionService
    {
        Task<List<Pillar>> GetPillarsAsync();
        Task<List<Question>> GetQuestionsAsync();
        Task<Question> AddQuestionAsync(Question q);
        Task<Question> EditQuestionAsync(int id, Question q);
        Task<bool> DeleteQuestionAsync(int id);
    }
} 