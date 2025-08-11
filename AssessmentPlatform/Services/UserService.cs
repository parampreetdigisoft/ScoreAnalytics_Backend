using AssessmentPlatform.Data;
using AssessmentPlatform.Models;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Common.Implementation;

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
            var user = _context.Users.First(u => u.UserID == request.UserID);

            var query =
            from u in _context.Users
            where u.Role == request.UserRole && (u.CreatedBy == request.UserID || user.Role==0)
            join uc in _context.UserCityMappings.Where(x => !x.IsDeleted)
                on u.UserID equals uc.UserId into userCityJoin
            from uc in userCityJoin.DefaultIfEmpty()
            join c in _context.Cities.Where(x => x.IsActive)
                on uc.CityId equals c.CityID into cityJoin
            from c in cityJoin.DefaultIfEmpty()
            join creator in _context.Users
                on u.CreatedBy equals creator.UserID into creatorJoin
            from creator in creatorJoin.DefaultIfEmpty()
            select new GetUserByRoleResponse
            {
                UserID = u.UserID,
                AssignCity = c.CityName,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role.ToString(),
                CreatedBy = creator.UserID,
                IsDeleted = u.IsDeleted,
                IsEmailConfirmed = u.IsEmailConfirmed,
                CreatedAt = u.CreatedAt,
                CreatedByName = creator.FullName
            };

            var response = await query.ApplyPaginationAsync(
                request,
                x => string.IsNullOrEmpty(request.SearchText) ||
                     x.Email.Contains(request.SearchText) ||
                     x.FullName.Contains(request.SearchText));

            return response;
        }
    }
}