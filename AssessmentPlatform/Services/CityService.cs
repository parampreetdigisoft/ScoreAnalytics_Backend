using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
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

        public async Task<ResultResponseDto<City>> AddCityAsync(AddUpdateCityDto q)
        {
            var existCity = await _context.Cities.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.CityName == x.CityName && x.State == q.State);
            if(existCity != null)
            {
                return ResultResponseDto<City>.Failure(new string[] { "City already exists" });
            }
            var city = new City
            {
                CityID=0,
                CityName=q.CityName,
                CreatedDate=DateTime.Now,
                State = q.State,
                Region = q.Region
            };
            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            return ResultResponseDto<City>.Success(city, new string[] { "City added Successfully" });
        }

        public async Task<ResultResponseDto<bool>> DeleteCityAsync(int id)
        {
            var q = await _context.Cities.FindAsync(id);
            if (q == null) return ResultResponseDto<bool>.Failure(new string[] { "City not exists" });

            _context.Cities.Remove(q);
            await _context.SaveChangesAsync();
            return ResultResponseDto<bool>.Success(true, new string[] { "City deleted Successfully" });

        }

        public async Task<ResultResponseDto<City>> EditCityAsync(int id, AddUpdateCityDto q)
        {
            var existCity = await _context.Cities.FirstOrDefaultAsync(x => !x.IsActive && !x.IsDeleted && q.CityName == x.CityName && x.State == q.State);
            if (existCity != null)
            {
                return ResultResponseDto<City>.Failure(new string[] { "City already exists" });
            }
            var existing = await _context.Cities.FindAsync(id);
            if (existing == null) return ResultResponseDto<City>.Failure(new string[] { "City not exists" });
            existing.CityName = q.CityName;
            existing.UpdatedDate = DateTime.Now;
            existing.Region = q.Region;
            existing.State = q.State;
            _context.Cities.Update(existing);
            await _context.SaveChangesAsync();
           
           return ResultResponseDto<City>.Success(existing,new string[] { "City edited Successfully" });
        }
        public async Task<PaginationResponse<City>> GetCitiesAsync(PaginationRequest request)
        {
            var query = _context.Cities.Where(p => p.IsActive); 

            var response = await query.ApplyPaginationAsync(
                request,
                x => string.IsNullOrEmpty(request.SearchText) ||
                     x.CityName.Contains(request.SearchText) ||
                     x.State.Contains(request.SearchText)
            );

            return response;
        }
        public async Task<ResultResponseDto<City>> GetByIdAsync(int id)
        {
            var d = await _context.Cities.FirstAsync(x => x.CityID == id);
            return await Task.FromResult(ResultResponseDto<City>.Success(d,new string[] { "get successfully" }));
        }

        public async Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int assignedByUserId)
        {
            if(_context.UserCityMappings.Any(x => x.UserId == userId && x.CityId == cityId && x.AssignedByUserId == assignedByUserId))
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "City already assigned to user" }));
            }
            var user = _context.Users.Find(userId);

            if (user == null)
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
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

            return await Task.FromResult(ResultResponseDto<object>.Success(new string[] { "City assigned successfully" }));
        }

        public async Task<ResultResponseDto<object>> EditAssingCity(int id, int userId, int cityId, int assignedByUserId)
        {
            if (_context.UserCityMappings.Any(x => x.UserId == userId && x.CityId == cityId && x.AssignedByUserId == assignedByUserId))
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "City already assigned to user" }));
            }
            var userMapping = _context.UserCityMappings.Find(id);

            if (userMapping == null)
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
            }

            userMapping.UserId = userId;
            userMapping.CityId = cityId;
            userMapping.AssignedByUserId = assignedByUserId;
            _context.UserCityMappings.Update(userMapping);
            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto<object>.Success(new string[] { "Assigned city updated successfully" }));
        }

        public async Task<ResultResponseDto<object>> DeleteAssingCity(int id)
        {
            var userMapping = _context.UserCityMappings.Find(id);
            if (userMapping == null)
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
            }

            userMapping.IsDeleted = true;
            _context.UserCityMappings.Update(userMapping);
            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto<object>.Success(new string[] { "Assigned city deleted successfully" }));
        }
    }
}
