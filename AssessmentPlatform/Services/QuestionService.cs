using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly ApplicationDbContext _context;
        public QuestionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Pillar>> GetPillarsAsync()
        {
            return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<PaginationResponse<GetQuestionRespones>> GetQuestionsAsync(GetQuestionRequestDto request)
        {
            var query =
                from q in _context.Questions
                    .Include(q => q.Pillar)
                where !q.IsDeleted
                   && (!request.PillarID.HasValue || q.PillarID == request.PillarID.Value)
                select new GetQuestionRespones
                {
                    QuestionID = q.QuestionID,
                    QuestionText = q.QuestionText,
                    PillarID = q.PillarID,
                    PillarName = q.Pillar.PillarName,
                    DisplayOrder = q.DisplayOrder
                };

            var response = await query.ApplyPaginationAsync(request);

            return response;
        }

        public async Task<Question> AddQuestionAsync(Question q)
        {
            _context.Questions.Add(q);
            await _context.SaveChangesAsync();
            return q;
        }

        public async Task<Question> EditQuestionAsync(int id, Question q)
        {
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null) return null;
            existing.QuestionText = q.QuestionText;
            existing.PillarID = q.PillarID;
            existing.DisplayOrder = q.DisplayOrder;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteQuestionAsync(int id)
        {
            var q = await _context.Questions.FindAsync(id);
            if (q == null) return false;

            q.IsDeleted = true; 
            _context.Questions.Update(q);
            await _context.SaveChangesAsync();
            return true;
        }
    }
} 