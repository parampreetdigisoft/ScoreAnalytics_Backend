using AssessmentPlatform.Common.Implementation;
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

        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRole(GetUserByRoleRequestDto request)
        {
            var currentUser = _context.Users.First(u => u.UserID == request.UserID);

            var query =
                from u in _context.Users
                where u.Role == (currentUser.Role == UserRole.Admin ? request.GetUserRole : UserRole.Evaluator) && u.Role != UserRole.Admin
                join uc in _context.UserCityMappings.Where(x => !x.IsDeleted && (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin))
                    on u.UserID equals uc.UserId into userCityJoin
                from uc in userCityJoin.DefaultIfEmpty()
                join ab in _context.Users
                    on uc.AssignedByUserId equals ab.UserID into assignedByJoin
                from ab in assignedByJoin.DefaultIfEmpty()
                select new GetUserByRoleResponse
                {
                    UserID = u.UserID,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role.ToString(),
                    CreatedBy = uc.AssignedByUserId,
                    IsDeleted = u.IsDeleted,
                    IsEmailConfirmed = u.IsEmailConfirmed,
                    CreatedAt = u.CreatedAt,
                    CreatedByName = ab.FullName
                };


            // Apply pagination + search
            var response = await query.ApplyPaginationAsync(
                request,
                x => string.IsNullOrEmpty(request.SearchText) ||
                     x.Email.Contains(request.SearchText) ||
                     x.FullName.Contains(request.SearchText));


            response.Data = response.Data.GroupBy(x => x.UserID).Select(g => g.FirstOrDefault());

            // Get all cities for fetched users in one query
            var userIds = response.Data.Select(x => x.UserID).ToList();
            var cityMap = await _context.UserCityMappings
            .Where(x => !x.IsDeleted && userIds.Contains(x.UserId))
            .Join(_context.Cities,
                  cm => cm.CityId,
                  c => c.CityID,
                  (cm, c) => new {
                      cm.UserId,
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
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.City).ToList());

            foreach (var item in response.Data)
            {
                result.TryGetValue(item.UserID, out var cities);
                item.cities = cities ?? new List<AddUpdateCityDto>();
            }

            return response;
        }

    }
}