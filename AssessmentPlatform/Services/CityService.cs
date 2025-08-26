using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.UserDtos;
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

        public async Task<ResultResponseDto<string>> AddCityAsync(BulkAddCityDto request)
        {
            // Normalize input list
            var inputCities = request.Cities
                .Select(c => new { CityName = c.CityName.Trim(), State = c.State.Trim(), Region = c.Region?.Trim() })
                .Distinct()
                .ToList();

            // Get already existing cities (match by CityName + State)
            var existingCities = await _context.Cities
                .Where(x => x.IsActive && !x.IsDeleted &&
                            inputCities.Select(c => c.CityName.ToLower()).Contains(x.CityName.ToLower()) &&
                            inputCities.Select(c => c.State.ToLower()).Contains(x.State.ToLower()))
                .Select(x => new { x.CityName, x.State })
                .ToListAsync();

            var existingCityNames = existingCities
                .Select(x => $"{x.CityName}, {x.State}")
                .ToList();

            // Filter out cities that already exist
            var newCities = inputCities
                .Where(c => !existingCities.Any(e =>
                    e.CityName.Equals(c.CityName, StringComparison.OrdinalIgnoreCase) &&
                    e.State.Equals(c.State, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Add only non-existing cities
            foreach (var cityDto in newCities)
            {
                var city = new City
                {
                    CityID = 0,
                    CityName = cityDto.CityName,
                    State = cityDto.State,
                    Region = cityDto.Region,
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false
                };
                _context.Cities.Add(city);
            }

            await _context.SaveChangesAsync();

            // Build response message
            if (existingCityNames.Any() && newCities.Any())
            {
                return ResultResponseDto<string>.Success(
                    "",
                    new string[] { $"{string.Join(", ", existingCityNames)} already exist" }
                );
            }
            else if (existingCityNames.Any())
            {
                return ResultResponseDto<string>.Failure(
                    new string[] { $"{string.Join(", ", existingCityNames)} already exist" }
                );
            }
            else
            {
                return ResultResponseDto<string>.Success(
                    "",
                    new string[] { $"City added successfully" }
                );
            }
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
        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
            if (user == null)
            {
                return new PaginationResponse<CityResponseDto>();
            }

            IQueryable<CityResponseDto> cityQuery;

            if (user.Role == UserRole.Admin)
            {
                cityQuery = 
                    from c in _context.Cities
                    select new CityResponseDto
                    {
                        CityID = c.CityID,
                        State = c.State,
                        CityName = c.CityName,
                        PostalCode = c.PostalCode,
                        Region = c.Region,
                        IsActive = c.IsActive,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsDeleted = c.IsDeleted
                    }; 
            }
            else
            {
               cityQuery =
                from c in _context.Cities
                join cm in _context.UserCityMappings
                    .Where(x => !x.IsDeleted && x.UserID == request.UserId)
                    on c.CityID equals cm.CityID
                join u in _context.Users on cm.AssignedByUserId equals u.UserID
                select new CityResponseDto
                {
                    CityID = c.CityID,
                    State = c.State,
                    CityName = c.CityName,
                    PostalCode = c.PostalCode,
                    Region = c.Region,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted,
                    AssignedBy = u.FullName
                };
                
            }
            var response = await cityQuery.ApplyPaginationAsync(
                request,
                x => string.IsNullOrEmpty(request.SearchText) ||
                     x.CityName.Contains(request.SearchText) ||
                     x.State.Contains(request.SearchText)
            );

            return response;
        }
        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCityByUserId(int userId)
        {
            var user = _context.Users.FirstOrDefault(x => x.UserID == userId);

            if (user == null)
            {
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "Invalid user" });
            }

            IQueryable<UserCityMappingResponseDto> cityQuery;

            if (user.Role == UserRole.Admin)
            {
                cityQuery =
                    from c in _context.Cities
                    select new UserCityMappingResponseDto
                    {
                        CityID = c.CityID,
                        State = c.State,
                        CityName = c.CityName,
                        PostalCode = c.PostalCode,
                        Region = c.Region,
                        IsActive = c.IsActive,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsDeleted = c.IsDeleted
                    };
            }
            else
            {
                cityQuery =
                 from c in _context.Cities
                 join cm in _context.UserCityMappings
                     .Where(x => !x.IsDeleted && x.UserID == userId)
                     on c.CityID equals cm.CityID
                 join u in _context.Users on cm.AssignedByUserId equals u.UserID
                 select new UserCityMappingResponseDto
                 {
                     CityID = c.CityID,
                     State = c.State,
                     CityName = c.CityName,
                     PostalCode = c.PostalCode,
                     Region = c.Region,
                     IsActive = c.IsActive,
                     CreatedDate = c.CreatedDate,
                     UpdatedDate = c.UpdatedDate,
                     IsDeleted = c.IsDeleted,
                     AssignedBy = u.FullName,
                     UserCityMappingID = cm.UserCityMappingID
                 };
            }
            var result = await cityQuery.ToListAsync();

           return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new string[] { "get successfully" });
        }
        public async Task<ResultResponseDto<City>> GetByIdAsync(int id)
        {
            var d = await _context.Cities.FirstAsync(x => x.CityID == id);
            return await Task.FromResult(ResultResponseDto<City>.Success(d,new string[] { "get successfully" }));
        }

        public async Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int assignedByUserId)
        {
            if(_context.UserCityMappings.Any(x => x.UserID == userId && x.CityID == cityId && x.AssignedByUserId == assignedByUserId && !x.IsDeleted))
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
                UserID = userId,
                CityID = cityId,
                AssignedByUserId = assignedByUserId,
                Role = user.Role
            };
            _context.UserCityMappings.Add(mapping);

            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto<object>.Success(new {},new string[] { "City assigned successfully" }));
        }

        public async Task<ResultResponseDto<object>> EditAssingCity(int id, int userId, int cityId, int assignedByUserId)
        {
            if (_context.UserCityMappings.Any(x => x.UserID == userId && x.CityID == cityId && x.AssignedByUserId == assignedByUserId))
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "City already assigned to user" }));
            }
            var userMapping = _context.UserCityMappings.Find(id);

            if (userMapping == null)
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "Invalid request data." }));
            }

            userMapping.UserID = userId;
            userMapping.CityID = cityId;
            userMapping.AssignedByUserId = assignedByUserId;
            _context.UserCityMappings.Update(userMapping);
            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto<object>.Success(new {},new string[] { "Assigned city updated successfully" }));
        }

        public async Task<ResultResponseDto<object>> UnAssignCity(UserCityUnMappingRequestDto requestDto)
        {
            var userMapping = _context.UserCityMappings.Where(x => x.UserID == requestDto.UserId && x.AssignedByUserId == requestDto.AssignedByUserId && !x.IsDeleted).ToList();
            if (userMapping == null && userMapping?.Count==0)
            {
                return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "user has no assign city" }));
            }
            foreach (var m in userMapping)
            {
                m.IsDeleted = true;
                _context.UserCityMappings.Update(m);
            }

            await _context.SaveChangesAsync();

            return await Task.FromResult(ResultResponseDto<object>.Success(new {},new string[] { "Assigned city deleted successfully" }));
        }
    }
}
