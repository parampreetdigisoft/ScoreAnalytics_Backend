using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class CityService : ICityService
    {
        private readonly ApplicationDbContext _context;
        public CityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<City> AddCityAsync(City q)
        {
            _context.Cities.Add(q);
            await _context.SaveChangesAsync();
            return q;
        }

        public async Task<bool> DeleteCityAsync(int id)
        {
            var q = await _context.Cities.FindAsync(id);
            if (q == null) return false;
            _context.Cities.Remove(q);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<City> EditCityAsync(int id, City q)
        {
            var existing = await _context.Cities.FindAsync(id);
            if (existing == null) return null;
            existing.CityName = q.CityName;
            existing.UpdatedDate = DateTime.Now;
            existing.PerformanceTier = q.PerformanceTier;
            existing.Region = q.Region;
            existing.IsActive = q.IsActive;
            _context.Cities.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
        public async Task<List<City>> GetCitiesAsync()
        {
            return await _context.Cities.Where(p => p.IsActive).ToListAsync();
        }
        public async Task<City> GetByIdAsync(int id)
        {
            var d = await _context.Cities.FirstAsync(x => x.CityID == id);
            return d;
        }
    }
}
