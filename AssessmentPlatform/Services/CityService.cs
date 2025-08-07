using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
            existing.PostalCode = q.PostalCode;
            existing.Region = q.Region;
            existing.State = q.State;
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

        public async Task<ResultResponseDto> AssingCityToUser(int userId, int cityId, int assignedByUserId)
        {
            if(_context.UserCityMappings.Any(x => x.UserId == userId && x.CityId == cityId && x.AssignedByUserId == assignedByUserId))
            {
                return await Task.FromResult(ResultResponseDto.Failure(new string[] { "City already assigned to user" }));
            }
            var user = _context.Users.Find(userId);

            if (user == null)
            {
                return await Task.FromResult(ResultResponseDto.Failure(new string[] { "Invalid request data." }));
            }
            var mapping = new UserCityMapping
            {
                UserId = userId,
                CityId = cityId,
                AssignedByUserId = assignedByUserId,
                Role = user.Role
            };
            _context.UserCityMappings.Add(mapping);

            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto.Success(new string[] { "City assigned successfully" }));
        }

        public async Task<ResultResponseDto> EditAssingCity(int id, int userId, int cityId, int assignedByUserId)
        {
            if (_context.UserCityMappings.Any(x => x.UserId == userId && x.CityId == cityId && x.AssignedByUserId == assignedByUserId))
            {
                return await Task.FromResult(ResultResponseDto.Failure(new string[] { "City already assigned to user" }));
            }
            var userMapping = _context.UserCityMappings.Find(id);

            if (userMapping == null)
            {
                return await Task.FromResult(ResultResponseDto.Failure(new string[] { "Invalid request data." }));
            }

            userMapping.UserId = userId;
            userMapping.CityId = cityId;
            userMapping.AssignedByUserId = assignedByUserId;
            _context.UserCityMappings.Update(userMapping);
            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto.Success(new string[] { "Assigned city updated successfully" }));
        }

        public async Task<ResultResponseDto> DeleteAssingCity(int id)
        {
            var userMapping = _context.UserCityMappings.Find(id);
            if (userMapping == null)
            {
                return await Task.FromResult(ResultResponseDto.Failure(new string[] { "Invalid request data." }));
            }

            userMapping.IsDeleted = true;
            _context.UserCityMappings.Update(userMapping);
            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto.Success(new string[] { "Assigned city deleted successfully" }));
        }
    }
}
