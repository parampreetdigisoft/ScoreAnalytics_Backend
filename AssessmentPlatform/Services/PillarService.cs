using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class PillarService : IPillarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public PillarService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        public async Task<List<Pillar>> GetAllAsync()
        {
            try
            {
                return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<Pillar>();
            }

        }

        public async Task<Pillar> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Pillars.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> AddAsync(Pillar pillar)
        {
            try
            {
                _context.Pillars.Add(pillar);
                await _context.SaveChangesAsync();
                return pillar;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar)
        {
            try
            {
                var existing = await _context.Pillars.FindAsync(id);
                if (existing == null) return null;
                existing.PillarName = pillar.PillarName;
                existing.Description = pillar.Description;
                existing.DisplayOrder = pillar.DisplayOrder;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return new Pillar();
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var pillar = await _context.Pillars.FindAsync(id);
                if (pillar == null) return false;
                _context.Pillars.Remove(pillar);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return false;
            }

        }
    }
}