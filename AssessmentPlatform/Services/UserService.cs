using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class UserService : IUserService
    {
        private readonly IAppLogger _appLogger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public UserService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
        }
        public User GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }
        public async Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRoleWithAssignedCity(GetUserByRoleRequestDto request)
        {
            try
            {
                var currentUser = _context.Users.First(u => u.UserID == request.UserID);

                var filteredMappings =
                    _context.UserCityMappings
                        .Where(x => !x.IsDeleted &&
                               (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin));

                Expression<Func<User, bool>> predicate = currentUser.Role switch
                {
                    UserRole.Admin => x => !x.IsDeleted && request.GetUserRole.HasValue ? x.Role == request.GetUserRole : (x.Role == UserRole.Evaluator || x.Role == UserRole.CityUser),
                    _ => x => !x.IsDeleted && x.Role == UserRole.Evaluator
                };

                // Build one-row-per-user by taking at most 1 mapping row per user
                // NOTE: use a deterministic column to order (e.g., CreatedAt or primary key).
                var query =
                    from u in _context.Users.Where(predicate)
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
                var userIds = response.Data.Select(x => x.UserID).Distinct().ToList();
                var cityMap = await _context.UserCityMappings
                .Where(x => !x.IsDeleted && userIds.Contains(x.UserID) && (x.AssignedByUserId == request.UserID || currentUser.Role == UserRole.Admin))
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
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUserByRoleWithAssignedCity", ex);
                return new PaginationResponse<GetUserByRoleResponse>();
            }
        }
        public async Task<ResultResponseDto<List<PublicUserResponse>>> GetEvaluatorByAnalyst(GetAssignUserDto request)
        {
            try
            {
                var query =
                    from uc in _context.UserCityMappings
                    where !uc.IsDeleted
                          && uc.AssignedByUserId == request.UserID
                          && (!request.SearchedUserID.HasValue || uc.UserID == request.SearchedUserID.Value)
                          && (!request.CityID.HasValue || uc.CityID == request.CityID.Value)
                    join u in _context.Users
                        .Where(x => !x.IsDeleted)
                        on uc.UserID equals u.UserID
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

                var users = await query
                    .Distinct()
                    .OrderBy(x => x.FullName)
                    .ToListAsync();

                return ResultResponseDto<List<PublicUserResponse>>
                    .Success(users, new[] { "User fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetEvaluatorByAnalyst", ex);
                return ResultResponseDto<List<PublicUserResponse>>
                    .Failure(new[] { "There is an error, please try later" });
            }
        }

        public async Task<ResultResponseDto<List<GetAssessmentResponseDto>>> GetUsersAssignedToCity(int cityId)
        {
            try
            {
                var year = DateTime.Now.Year;
                var query =
                from u in _context.Users
                where !u.IsDeleted
                join uc in _context.UserCityMappings
                        .Where(x => !x.IsDeleted && x.CityID == cityId)
                    on u.UserID equals uc.UserID
                join c in _context.Cities.Where(x => !x.IsDeleted)
                    on uc.CityID equals c.CityID
                join createdBy in _context.Users.Where(x => !x.IsDeleted)
                    on uc.AssignedByUserId equals createdBy.UserID into createdByUser
                from createdBy in createdByUser.DefaultIfEmpty()

                    // LEFT JOIN to Assessments
                join a in _context.Assessments
                        .Include(q => q.PillarAssessments)
                            .ThenInclude(q => q.Responses).Where(x=>x.IsActive && x.CreatedAt.Year == year)
                    on uc.UserCityMappingID equals a.UserCityMappingID into userAssessment
                from a in userAssessment.DefaultIfEmpty()

                select new GetAssessmentResponseDto
                {
                    AssessmentID = a != null ? a.AssessmentID : 0,
                    UserCityMappingID = uc.UserCityMappingID,
                    CreatedAt = a != null ? a.CreatedAt : null,
                    CityID = c.CityID,
                    CityName = c.CityName,
                    State = c.State,
                    UserID = u.UserID,
                    UserName = u.FullName,
                    Score = a != null
                        ? a.PillarAssessments.SelectMany(x => x.Responses)
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .Sum(r => (int?)r.Score ?? 0)
                        : 0,
                    AssignedByUser = createdBy != null ? createdBy.FullName : "",
                    AssignedByUserId = createdBy != null ? createdBy.UserID : 0,
                    AssessmentYear = a != null ? a.UpdatedAt.Year : 0,
                    AssessmentPhase = a != null ? a.AssessmentPhase : null
                };



                var users = await query.Distinct().ToListAsync();

                return ResultResponseDto<List<GetAssessmentResponseDto>>.Success(users, new[] { "user get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetUsersAssignedToCity", ex);
                return ResultResponseDto<List<GetAssessmentResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<UpdateUserResponseDto>> GetUserInfo(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ResultResponseDto<UpdateUserResponseDto>.Failure(new List<string>() { "Invalid request " });

                var response = new UpdateUserResponseDto
                {
                    UserID = user.UserID,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Email = user.Email,
                    ProfileImagePath = user?.ProfileImagePath,
                    Is2FAEnabled = user?.Is2FAEnabled ?? false,
                    Tier = user?.Tier ?? Enums.TieredAccessPlan.Pending
                };

                return ResultResponseDto<UpdateUserResponseDto>.Success(response, new List<string> { "Updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure UpdateUser", ex);
                return ResultResponseDto<UpdateUserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
    }
}