using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;

namespace AssessmentPlatform.Services
{
    public class PillarService : IPillarService
    {
        private readonly ApplicationDbContext _context;
        public PillarService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Pillar>> GetAllAsync()
        {
            return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
        }

        public async Task<Pillar> GetByIdAsync(int id)
        {
            return await _context.Pillars.FindAsync(id);
        }

        public async Task<Pillar> AddAsync(Pillar pillar)
        {
            _context.Pillars.Add(pillar);
            await _context.SaveChangesAsync();
            return pillar;
        }

        public async Task<Pillar> UpdateAsync(int id, Pillar pillar)
        {
            var existing = await _context.Pillars.FindAsync(id);
            if (existing == null) return null;
            existing.PillarName = pillar.PillarName;
            existing.Description = pillar.Description;
            existing.DisplayOrder = pillar.DisplayOrder;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var pillar = await _context.Pillars.FindAsync(id);
            if (pillar == null) return false;
            _context.Pillars.Remove(pillar);
            await _context.SaveChangesAsync();
            return true;
        }
    }
} 