using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }
        public User GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }
        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCity(GetUserByRoleRequestDto request)
        {
            var currentUser = _context.Users.First(u => u.UserID == request.UserID);
  
            var filteredMappings =
                _context.UserCityMappings
                    .Where(x => !x.IsDeleted &&
                           (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin));

            // Build one-row-per-user by taking at most 1 mapping row per user
            // NOTE: use a deterministic column to order (e.g., CreatedAt or primary key).
            var query =
                from u in _context.Users
                where u.Role == (currentUser.Role == UserRole.Admin ? request.GetUserRole : UserRole.Evaluator)
                      && !u.IsDeleted
                from uc in filteredMappings
                            .Where(m => m.UserID == u.UserID)
                            .Take(1)                              
                from ab in _context.Users
                            .Where(p => uc != null && p.UserID == uc.AssignedByUserId)
                            .DefaultIfEmpty()
                select new GetUserByRoleResponse
                {
                    UserID = u.UserID,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role.ToString(),
                    CreatedBy = uc != null ? uc.AssignedByUserId : null,
                    IsDeleted = u.IsDeleted,
                    IsEmailConfirmed = u.IsEmailConfirmed,
                    CreatedAt = u.CreatedAt,
                    CreatedByName = ab != null ? ab.FullName : null
                };


            // Apply pagination + search
            var response = await query.ApplyPaginationAsync(
                request,
                x => string.IsNullOrEmpty(request.SearchText) ||
                     x.Email.Contains(request.SearchText) ||
                     x.FullName.Contains(request.SearchText));

            // Get all cities for fetched users in one query
            var userIds = response.Data.Select(x => x.UserID).ToList();
            var cityMap = await _context.UserCityMappings
            .Where(x => !x.IsDeleted && userIds.Contains(x.UserID) && x.AssignedByUserId == request.UserID)
            .Join(_context.Cities,
                  cm => cm.CityID,
                  c => c.CityID,
                  (cm, c) => new
                  {
                      cm.UserID,
                      City = new AddUpdateCityDto
                      {
                          CityID = c.CityID,
                          CityName = c.CityName,
                          Region = c.Region,
                          State = c.State
                      }
                  })
            .ToListAsync();

            var result = cityMap
                .GroupBy(x => x.UserID)
                .ToDictionary(g => g.Key, g => g.Select(x => x.City).ToList());

            foreach (var item in response.Data)
            {
                result.TryGetValue(item.UserID, out var cities);
                item.cities = cities ?? new List<AddUpdateCityDto>();
            }

            return response;
        }

        public async Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto request)
        {
            var query =
                from u in _context.Users
                where !u.IsDeleted
                join uc in _context.UserCityMappings.Where(x => !x.IsDeleted && x.AssignedByUserId == request.UserID && (!request.SearchedUserID.HasValue || x.UserID == request.SearchedUserID))
                    on u.UserID equals uc.UserID
                select new PublicUserResponse
                {
                    UserID = u.UserID,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role.ToString(),
                    CreatedBy = uc.AssignedByUserId,
                    IsDeleted = u.IsDeleted,
                    IsEmailConfirmed = u.IsEmailConfirmed,
                    CreatedAt = u.CreatedAt
                };

            var users = await query.Distinct().ToListAsync();

            return ResultResponseDto<List<PublicUserResponse>>.Success(users, new[] { "user get successfully" });
        }

    }
}