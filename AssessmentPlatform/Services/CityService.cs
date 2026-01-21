using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class CityService : ICityService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly ICommonService _commonService;
        public CityService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _commonService = commonService;
        }

        #endregion

        #region  methods Implementations
        public async Task<ResultResponseDto<string>> AddUpdateCity(AddUpdateCityDto q)
        {
            try
            {
                string image = string.Empty;
                if (q.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "assets/cities");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // ?? Remove old image if exists
                    if (!string.IsNullOrEmpty(q.ImageUrl))
                    {
                        string oldFilePath = Path.Combine(_env.WebRootPath, q.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(q.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await q.ImageFile.CopyToAsync(stream);
                    }

                    image = "/assets/cities/" + fileName;
                }
                if(q.CityID > 0)
                {
                    var existCity = await _context.Cities.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.CityName == x.CityName && x.State == q.State && x.CityID != q.CityID);
                    if (existCity != null)
                    {
                        return ResultResponseDto<string>.Failure(new string[] { "City already exists" });
                    }

                    var existing = await _context.Cities.FindAsync(q.CityID);
                    if (existing == null) return ResultResponseDto<string>.Failure(new string[] { "City not exists" });
                    existing.CityName = q.CityName;
                    existing.UpdatedDate = DateTime.Now;
                    existing.Region = q.Region;
                    existing.State = q.State;
                    existing.PostalCode = q.PostalCode;
                    if (!string.IsNullOrEmpty(image))
                    {
                        existing.Image = image;
                    }
                    existing.Country = q.Country;
                    existing.Latitude = q.Latitude;
                    existing.Longitude = q.Longitude;
                    _context.Cities.Update(existing);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<string>.Success("", new string[] { "City edited Successfully" });
                }
                else
                {
                    var payload = new BulkAddCityDto { Cities = new() { q } };
                    var response = await AddBulkCityAsync(payload, image);
                    return response;
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UnAssignCity", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<string>> AddBulkCityAsync(BulkAddCityDto request, string image="")
        {
            try
            {
                // Normalize input list
                var inputCities = request.Cities
                    .Select(c => new { Country = c.Country,PostalCode = c.PostalCode, CityName = c.CityName.Trim(), State = c.State.Trim(), Region = c.Region?.Trim(), Longitude = c.Longitude, Latitude = c.Latitude })
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
                        Country = cityDto.Country,
                        CityName = cityDto.CityName,
                        State = cityDto.State,
                        Region = cityDto.Region,
                        CreatedDate = DateTime.Now,
                        PostalCode = cityDto.PostalCode,
                        IsActive = true,
                        IsDeleted = false,
                        Image = image,
                        Longitude = cityDto.Longitude,
                        Latitude = cityDto.Latitude
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
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddCityAsync", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<bool>> DeleteCityAsync(int id)
        {
            try
            {
                var q = await _context.Cities.FindAsync(id);
                if (q == null) return ResultResponseDto<bool>.Failure(new string[] { "City not exists" });

                _context.Cities.Remove(q);
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new string[] { "City deleted Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in DeleteCityAsync", ex);
                return ResultResponseDto<bool>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<City>> EditCityAsync(int id, AddUpdateCityDto q)
        {

            try
            {
                var existCity = await _context.Cities.FirstOrDefaultAsync(x => x.IsActive && !x.IsDeleted && q.CityName == x.CityName && x.State == q.State && x.CityID != id);
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

                return ResultResponseDto<City>.Success(existing, new string[] { "City edited Successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in EditCityAsync", ex);
                return ResultResponseDto<City>.Failure(new string[] { "There is an error please try later" });
            }
        }

        #region GetCitiesAsync
        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request, UserRole role)
        {
            try
            {
                int year = DateTime.UtcNow.Year;

                IQueryable<CityResponseDto> query = role == UserRole.Admin
                    ? GetAdminCityQuery(year)
                    : GetUserCityQuery(request.UserId, year);

                // 🔍 Search
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.Trim();
                    query = query.Where(x =>
                        x.CityName.Contains(search) ||
                        x.State.Contains(search));
                }

                // 📄 Pagination (DB level)
                var response = await query.ApplyPaginationAsync(request);

                // 🧮 Manual Score Calculation (Non-City User)
                if (role != UserRole.CityUser && response.Data.Any())
                {
                    await ApplyManualScoresAsync(response, request, role, year);
                }

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCitiesAsync", ex);
                return new PaginationResponse<CityResponseDto>();
            }
        }
        private IQueryable<UserCityMappingResponseDto> GetAdminCityQuery(int year)
        {
            return
                from c in _context.Cities.AsNoTracking()
                where !c.IsDeleted
                join ai in _context.AICityScores
                        .Where(x => x.IsVerified && x.Year == year)
                    on c.CityID equals ai.CityID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()
                select new UserCityMappingResponseDto
                {
                    CityID = c.CityID,
                    CityName = c.CityName,
                    State = c.State,
                    PostalCode = c.PostalCode,
                    Region = c.Region,
                    Country = c.Country,
                    Image = c.Image,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted,
                    Score = 0,
                    AiScore = ai != null ? ai.AIScore : 0
                };
        }

        private IQueryable<UserCityMappingResponseDto> GetUserCityQuery(long? userId, int? year)
        {
            year = year ?? DateTime.Now.Year;

            return
                from c in _context.Cities.AsNoTracking()
                join cm in _context.UserCityMappings
                        .Where(x => !x.IsDeleted && x.UserID == userId)
                    on c.CityID equals cm.CityID
                join u in _context.Users
                    on cm.AssignedByUserId equals u.UserID
                join ai in _context.AICityScores
                .Where(x => x.IsVerified && x.Year == year)
            on c.CityID equals ai.CityID into aiJoin
                from ai in aiJoin.DefaultIfEmpty()

                where !c.IsDeleted
                select new UserCityMappingResponseDto
                {
                    CityID = c.CityID,
                    State = c.State,
                    CityName = c.CityName,
                    Country = c.Country,
                    PostalCode = c.PostalCode,
                    Region = c.Region,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate,
                    UpdatedDate = c.UpdatedDate,
                    IsDeleted = c.IsDeleted,
                    AssignedBy = u.FullName,
                    UserCityMappingID = cm.UserCityMappingID,
                    Score = 0,
                    AiScore = ai.AIScore
                };
        }
        private async Task ApplyManualScoresAsync(PaginationResponse<CityResponseDto> response,PaginationRequest request,UserRole role, int year)
        {
            var scores = await _commonService.GetCitiesProgressAsync(request.UserId.GetValueOrDefault(),(int)role, year);

            var scoreMap = scores
                .GroupBy(x => x.CityID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(x => (decimal?)x.ScoreProgress) ?? 0);

            foreach (var city in response.Data)
            {
                if (scoreMap.TryGetValue(city.CityID, out var score))
                {
                    city.Score = score;
                }
            }

            // ✅ Correct dynamic sorting
            response.Data = request.SortDirection?.ToLower() == "desc"
                ? response.Data.OrderByDescending(x => x.Score)
                : response.Data.OrderBy(x => x.Score);
        }
        
        #endregion
        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCityByUserId(int userId, UserRole userRole)
        {
            try
            {

                IQueryable<UserCityMappingResponseDto> cityQuery;

                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Step 1️⃣: Fetch city score averages as a dictionary
                var cityScoresQuery =
                   from ar in _context.AICityScores
                   where ((ar.UpdatedAt >= startDate && ar.UpdatedAt < endDate))
                   group ar by ar.CityID into g
                   select new
                   {
                       CityID = g.Key,
                       Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                   };

                if (userRole == UserRole.Admin)
                {
                    cityQuery = GetAdminCityQuery(year);
                }
                else
                {
                    cityQuery = GetUserCityQuery(userId, year);
                }
                var result = await cityQuery.ToListAsync();

                if (userRole != UserRole.CityUser)
                {
                    var scores = await _commonService.GetCitiesProgressAsync(userId, (int)userRole, year);

                    var scoreMap = scores
                        .GroupBy(x => x.CityID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Average(x => (decimal?)x.ScoreProgress) ?? 0);

                    foreach (var city in result)
                    {
                        if (scoreMap.TryGetValue(city.CityID, out var score))
                        {
                            city.Score = score;
                        }
                    }

                }
                result = (userRole == UserRole.CityUser ? result.OrderByDescending(x => x.AiScore) : result.OrderByDescending(x => x.Score)).ToList();

                return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCityByUserId", ex);
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<City>> GetByIdAsync(int id)
        {
            try
            {
                var d = await _context.Cities.FirstAsync(x => x.CityID == id);
                return await Task.FromResult(ResultResponseDto<City>.Success(d, new string[] { "get successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return ResultResponseDto<City>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> AssingCityToUser(int userId, int cityId, int assignedByUserId)
        {
            try
            {
                if (_context.UserCityMappings.Any(x => x.UserID == userId && x.CityID == cityId && x.AssignedByUserId == assignedByUserId && !x.IsDeleted))
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

                return await Task.FromResult(ResultResponseDto<object>.Success(new { }, new string[] { "City assigned successfully" }));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AssingCityToUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> EditAssingCity(int id, int userId, int cityId, int assignedByUserId)
        {
            try
            {

                if (_context.UserCityMappings.Any(x => x.UserID == userId && x.CityID == cityId && x.AssignedByUserId == assignedByUserId))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "City already assigned to user" });
                }
                var userMapping = _context.UserCityMappings.Find(id);

                if (userMapping == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }

                userMapping.UserID = userId;
                userMapping.CityID = cityId;
                userMapping.AssignedByUserId = assignedByUserId;
                _context.UserCityMappings.Update(userMapping);
                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned city updated successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> UnAssignCity(UserCityUnMappingRequestDto requestDto)
        {
            try
            {
                var userMapping = _context.UserCityMappings.Where(x => x.UserID == requestDto.UserId && x.AssignedByUserId == requestDto.AssignedByUserId && !x.IsDeleted).ToList();
                if (userMapping == null && userMapping?.Count == 0)
                {
                    return await Task.FromResult(ResultResponseDto<object>.Failure(new string[] { "user has no assign city" }));
                }
                foreach (var m in userMapping)
                {
                    m.IsDeleted = true;
                    _context.UserCityMappings.Update(m);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "Assigned city deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UnAssignCity", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> GetCityByUserIdForAssessment(int userId)
        {
            try
            {

                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userId);

                if (user == null)
                {
                    return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "Invalid user" });
                }
                var year = DateTime.Now.Year;
                Expression<Func<Assessment, bool>>  predicate = a => 
                !a.UserCityMapping.IsDeleted 
                && a.UserCityMapping.UserID == userId 
                && a.UpdatedAt.Year == year
                && (a.AssessmentPhase == AssessmentPhase.Completed || a.AssessmentPhase == AssessmentPhase.EditRejected || a.AssessmentPhase == AssessmentPhase.EditRequested);

                // Get distinct UserCityMappings which are not show to user
                var userCityMappingIds = _context.Assessments
                    .Where(predicate)
                    .Select(a => a.UserCityMappingID)
                    .Distinct();

                // Project into response DTO
                var cityQuery =
                     from c in _context.Cities
                     join cm in _context.UserCityMappings
                         .Where(x => !x.IsDeleted && x.UserID == userId && !userCityMappingIds.Contains(x.UserCityMappingID))
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

                var result = await cityQuery.ToListAsync();

                if (!result.Any())
                {
                    return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "No city is found for assessment" });
                }

                return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new string[] { "Retrieved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityByUserIdForAssessment", ex);
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userID, DateTime updatedAt, UserRole userRole)
        {
            try
            {
                var cityHistory = new CityHistoryDto();

                Expression<Func<UserCityMapping, bool>> predicate;

                if (userRole == UserRole.Analyst)
                    predicate = x => !x.IsDeleted && (x.AssignedByUserId == userID || x.UserID == userID);
                else if(userRole == UserRole.Evaluator)
                    predicate = x => !x.IsDeleted && x.UserID == userID;
                else
                    predicate = x => !x.IsDeleted;

                // 1️⃣ Get city-related counts in a single round trip
                var cityQuery = await (
                    from c in _context.Cities
                    where !c.IsDeleted && c.IsActive
                    join uc in _context.UserCityMappings.Where(predicate)
                        on c.CityID equals uc.CityID into cityMappings
                    from uc in cityMappings.DefaultIfEmpty()
                    join a in _context.Assessments.Where(x => x.IsActive && x.UpdatedAt.Year == updatedAt.Year)
                        on uc.UserCityMappingID equals a.UserCityMappingID into cityAssessments
                    from a in cityAssessments.DefaultIfEmpty()
                    select new
                    {
                        c.CityID,
                        HasMapping = uc != null,
                        IsCompleted = a != null && a.AssessmentPhase == AssessmentPhase.Completed
                    }
                ).ToListAsync();

                cityHistory.TotalCity = cityQuery.Select(x => x.CityID).Distinct().Count();
                cityHistory.ActiveCity = cityQuery.Where(x => x.HasMapping).Select(x => x.CityID).Distinct().Count();
                cityHistory.CompeleteCity = cityQuery.Where(x => x.IsCompleted).Select(x => x.CityID).Distinct().Count();
                cityHistory.InprocessCity = cityHistory.ActiveCity - cityHistory.CompeleteCity;

                // 2️⃣ Get evaluators & analysts in a single query
                var userCounts = await _context.Users
                    .Where(u => !u.IsDeleted && (u.Role == UserRole.Evaluator || u.Role == UserRole.Analyst))
                    .GroupBy(u => u.Role)
                    .Select(g => new { Role = g.Key, Count = g.Count() })
                    .ToListAsync();

                cityHistory.TotalEvaluator = userCounts.FirstOrDefault(x => x.Role == UserRole.Evaluator)?.Count ?? 0;
                cityHistory.TotalAnalyst = userCounts.FirstOrDefault(x => x.Role == UserRole.Analyst)?.Count ?? 0;

                return ResultResponseDto<CityHistoryDto>.Success(
                    cityHistory,
                    new List<string> { "Get history successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityHistory", ex);
                return ResultResponseDto<CityHistoryDto>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID, DateTime updatedAt)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID && x.Role != UserRole.CityUser);
                if(user == null)
                {
                    return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Failure(new string[] { "Invalid request" });
                }

                // Get total pillars and questions (independent query)
                var pillarStats = await _context.Pillars
                    .Select(p => new { QuestionsCount = p.Questions.Count() })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                Expression<Func<UserCityMapping, bool>> predicate;

                if (user.Role == UserRole.Analyst)
                    predicate = x => !x.IsDeleted && (x.AssignedByUserId == userID || x.UserID == userID);
                else
                    predicate = x => !x.IsDeleted && x.UserID == userID;


                var cityRaw = await (
                    from uc in _context.UserCityMappings.Where(predicate)
                    join c in _context.Cities.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.CityID equals c.CityID
                    join a in _context.Assessments.Where(x => x.IsActive && x.UpdatedAt.Year == updatedAt.Year)
                        on uc.UserCityMappingID equals a.UserCityMappingID into cityAssessments
                    from a in cityAssessments.DefaultIfEmpty()
                    select new
                    {
                        c.CityID,
                        c.CityName,
                        UserCityMapping = uc,
                        AssessmentID = (int?)a.AssessmentID,
                        a.PillarAssessments,
                        Responses = a.PillarAssessments.SelectMany(pa => pa.Responses)
                    }
                ).AsNoTracking().ToListAsync();  // 🚀 force materialization first

                // Now do grouping/aggregation in memory (LINQ to Objects)
                var citySubmission = cityRaw
                    .GroupBy(g => new { g.CityID, g.CityName })
                    .Select(g =>
                    {
                        var allPillars = g.Where(x => x.PillarAssessments != null).SelectMany(p => p.PillarAssessments);
                        var allResponses = g.Where(x=> x.Responses != null).SelectMany(p => p.Responses);
                        var userCityMappingCount = g.Select(x => x.UserCityMapping).Count();

                        var scoreList = allResponses.Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                                .Select(r => (int?)r.Score ?? 0);

                        return new GetCitiesSubmitionHistoryReponseDto
                        {
                            CityID = g.Key.CityID,
                            CityName = g.Key.CityName,
                            TotalAssessment = g.Select(x => x.AssessmentID).Where(id => id.HasValue).Distinct().Count(),
                            Score = allResponses.Sum(r => (int?)r.Score ?? 0),
                            TotalPillar = totalPillars * userCityMappingCount,
                            TotalAnsPillar = allPillars.Count(),
                            TotalQuestion = totalQuestions * userCityMappingCount,
                            AnsQuestion = allResponses.Count(),
                            ScoreProgress = scoreList.Count() == 0 ? 0m : (scoreList.Sum() * 100) / (scoreList.Count() * 4)
                        };
                    })
                    .ToList();


                return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Success(citySubmission ?? new(), new List<string> { "Get Cities history successfully" });

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCitiesProgressByUserId", ex);
                return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> getAllCityByLocation(GetNearestCityRequestDto r)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == r.UserID);
                if (user == null)
                    return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new[] { "Invalid user" });

                var year = DateTime.Now.Year;

                Expression<Func<Assessment, bool>> predicate = a =>
                    !a.UserCityMapping.IsDeleted &&
                    a.UserCityMapping.UserID == r.UserID &&
                    a.UpdatedAt.Year == year &&
                    (a.AssessmentPhase == AssessmentPhase.Completed ||
                     a.AssessmentPhase == AssessmentPhase.EditRejected ||
                     a.AssessmentPhase == AssessmentPhase.EditRequested);

                var userCityMappingIds = await _context.Assessments
                    .Where(predicate)
                    .Select(a => a.UserCityMappingID)
                    .Distinct()
                    .ToListAsync();

                // First get data from DB (no static method call inside query)
                var cityList = await (
                    from c in _context.Cities
                    join cm in _context.UserCityMappings
                        .Where(x => !x.IsDeleted && x.UserID == r.UserID && !userCityMappingIds.Contains(x.UserCityMappingID))
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
                        UserCityMappingID = cm.UserCityMappingID,
                        Latitude = c.Latitude,
                        Longitude = c.Longitude
                    }).ToListAsync();

                // Then calculate distance in memory using static method
                foreach (var city in cityList)
                {
                    if (city.Latitude.HasValue && city.Longitude.HasValue)
                        city.Distance = HaversineDistance(r.Latitude, r.Longitude, city.Latitude.Value, city.Longitude.Value);
                    else
                        city.Distance = double.MaxValue;
                }

                var result = cityList.OrderBy(x => x.Distance).ToList();

                if (!result.Any())
                    return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new[] { "No city is found for assessment" });

                return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new[] { "Retrieved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAllCityByLocation", ex);
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new[] { "An error occurred, please try later" });
            }
        }

        private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // Radius of Earth in km
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            lat1 = lat1 * Math.PI / 180.0;
            lat2 = lat2 * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Asin(Math.Sqrt(a));
            return R * c;
        }
        
        public async Task<ResultResponseDto<List<UserCityMappingResponseDto>>> GetAiAccessCity(int userId, UserRole userRole)
        {
            try
            {
                IQueryable<UserCityMappingResponseDto> cityQuery;

                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Step 1️⃣: Fetch city score averages as a dictionary
                var cityScoresQuery =
                   from ar in _context.AICityScores
                   where ar.UpdatedAt >= startDate && ar.UpdatedAt < endDate
                   group ar by ar.CityID into g
                   select new
                   {
                       CityID = g.Key,
                       Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                   };

                cityQuery =
                    from c in _context.Cities
                    join cm in _context.AIUserCityMappings
                        .Where(x => x.IsActive && x.UserID == userId)
                        on c.CityID equals cm.CityID
                    join u in _context.Users on cm.AssignBy equals u.UserID
                    join cs in cityScoresQuery on cm.CityID equals cs.CityID into scoreGroup
                    from cs in scoreGroup.DefaultIfEmpty()
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
                        UserCityMappingID = cm.AIUserCityMappingID,
                        Score = cs.Score,
                    };
                var result = await cityQuery
                    .OrderByDescending(x => x.Score)
                                .ToListAsync();

                return ResultResponseDto<List<UserCityMappingResponseDto>>.Success(result, new string[] { "get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in getAllCityByUserId", ex);
                return ResultResponseDto<List<UserCityMappingResponseDto>>.Failure(new string[] { "There is an error please try later" });
            }
        }
        #endregion
    }
}
