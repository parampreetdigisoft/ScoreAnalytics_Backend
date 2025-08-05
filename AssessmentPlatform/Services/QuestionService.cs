using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;

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

        public async Task<List<Question>> GetQuestionsAsync()
        {
            return await _context.Questions.Include(q => q.Pillar).OrderBy(q => q.DisplayOrder).ToListAsync();
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
            _context.Questions.Remove(q);
            await _context.SaveChangesAsync();
            return true;
        }
    }
} 