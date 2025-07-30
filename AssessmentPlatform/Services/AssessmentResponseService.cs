using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;

namespace AssessmentPlatform.Services
{
    public class AssessmentResponseService : IAssessmentResponseService
    {
        private readonly ApplicationDbContext _context;
        public AssessmentResponseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AssessmentResponse>> GetAllAsync()
        {
            return await _context.AssessmentResponses.ToListAsync();
        }

        public async Task<AssessmentResponse> GetByIdAsync(int id)
        {
            return await _context.AssessmentResponses.FindAsync(id);
        }

        public async Task<AssessmentResponse> AddAsync(AssessmentResponse response)
        {
            _context.AssessmentResponses.Add(response);
            await _context.SaveChangesAsync();
            return response;
        }

        public async Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response)
        {
            var existing = await _context.AssessmentResponses.FindAsync(id);
            if (existing == null) return null;
            existing.Score = response.Score;
            existing.Justification = response.Justification;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var resp = await _context.AssessmentResponses.FindAsync(id);
            if (resp == null) return false;
            _context.AssessmentResponses.Remove(resp);
            await _context.SaveChangesAsync();
            return true;
        }
    }
} 