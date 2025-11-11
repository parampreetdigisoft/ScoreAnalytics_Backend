
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class CityUserService : ICityUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public CityUserService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }
        public async Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userID)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID);
                if (user == null)
                {
                    return ResultResponseDto<CityHistoryDto>.Failure(new[] { "Invalid request" });
                }

                var date = DateTime.Now;
                var cityHistory = new CityHistoryDto();

                var assessments = await (
                    from c in _context.Cities
                    where !c.IsDeleted && c.IsActive
                    join uc in _context.UserCityMappings.Where(x => !x.IsDeleted)
                        on c.CityID equals uc.CityID into cityMappings
                    from uc in cityMappings.DefaultIfEmpty()
                    join a in _context.Assessments
                    .Include(x => x.PillarAssessments)
                    .ThenInclude(x => x.Responses)
                        .Where(x => x.IsActive && x.UpdatedAt.Year == date.Year)
                        on uc.UserCityMappingID equals a.UserCityMappingID into cityAssessments
                    from a in cityAssessments.DefaultIfEmpty()
                    select new
                    {
                        c.CityID,
                        HasMapping = uc != null,
                        Assessment = a
                    }
                ).ToListAsync(); // ✅ Bring data into memory first

                // Now compute scores safely in-memory
                var cityQuery = assessments.Select(x => new
                {
                    x.CityID,
                    x.HasMapping,
                    Score = x.Assessment != null
                        ? x.Assessment.PillarAssessments
                            .SelectMany(p => p.Responses)
                            .Sum(r => (int?)r.Score) // ✅ Safe enum → int cast
                        : 0
                }).ToList();

                var accessCity = _context.PublicUserCityMappings.Where(x => x.UserID == userID).Select(x=>x.CityID);
                // Then your aggregation logic
                cityHistory.TotalCity = cityQuery.Select(x => x.CityID).Distinct().Count();
                cityHistory.TotalAccessCity = accessCity.Count();
                cityHistory.ActiveCity = cityQuery.Where(x => x.HasMapping).Select(x => x.CityID).Distinct().Count();

                var cityScores = cityQuery
                    .GroupBy(x => x.CityID)
                    .Select(g => new {g.Key, Score = Convert.ToDecimal(g.Sum(s => s.Score)) / (g.Count() * 4) })
                    .ToList();

                if (cityScores.Any())
                {
                    cityHistory.AvgHighScore = cityScores.Where(x=> accessCity.Contains(x.Key)).Max(x=>x.Score);
                    cityHistory.AvgLowerScore = cityScores.Where(x => accessCity.Contains(x.Key)).Min(x => x.Score);
                    cityHistory.OverallVitalityScore = cityScores.Where(x => accessCity.Contains(x.Key)).Average(x => x.Score);
                }
                else
                {
                    cityHistory.AvgHighScore = 0;
                    cityHistory.AvgLowerScore = 0;
                }

                return ResultResponseDto<CityHistoryDto>.Success(
                    cityHistory,
                    new List<string> { "Get history successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCityHistory", ex);
                return ResultResponseDto<CityHistoryDto>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto)
        {
            try
            {
                var userID = userCityRequstDto.UserID;
                var cityID = userCityRequstDto.CityID;
                var currentYear = DateTime.Now.Year;

                // Validate user first
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.CityUser);

                if (user == null)
                {
                    return new GetCityQuestionHistoryReponseDto
                    {
                        CityID = cityID,
                        Score = 0,
                        TotalPillar = 0,
                        TotalAnsPillar = 0,
                        TotalQuestion = 0,
                        AnsQuestion = 0,
                        TotalAssessment = 0,
                        Pillars = new List<CityPillarQuestionHistoryReponseDto>()
                    };
                }

                // ✅ Fetch all relevant UserCityMapping IDs in one call
                var ucmIds = await _context.UserCityMappings
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CityID == cityID)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                if (!ucmIds.Any())
                {
                    return new GetCityQuestionHistoryReponseDto
                    {
                        CityID = cityID,
                        Score = 0,
                        TotalPillar = 0,
                        TotalAnsPillar = 0,
                        TotalQuestion = 0,
                        AnsQuestion = 0,
                        TotalAssessment = 0,
                        Pillars = new List<CityPillarQuestionHistoryReponseDto>()
                    };
                }
                var pillarPredicate = user.Tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 15
                };

                var choicePillarIds = _context.CityUserPillarMappings.Where(x => x.UserID == userID).Select(x=>x.PillarID);
                // ✅ Pre-fetch total pillars and questions
                var pillarStats = await _context.Pillars
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.ImagePath,
                        IsAccess = choicePillarIds.Contains(p.PillarID),
                        p.DisplayOrder,
                        QuestionCount = p.Questions.Count()
                    })
                    .OrderByDescending(x => x.IsAccess) 
                    .ThenBy(x => x.DisplayOrder)        
                    .ToListAsync();


                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionCount);

                // ✅ Fetch all active pillar assessments for those mappings this year
                var activePillarAssessments = await _context.PillarAssessments
                    .AsNoTracking()
                    .Where(pa => pa.Assessment.IsActive
                                 && pa.Assessment.UpdatedAt.Year == currentYear
                                 && ucmIds.Contains(pa.Assessment.UserCityMappingID))
                    .Select(pa => new
                    {
                        pa.PillarID,
                        pa.PillarAssessmentID,
                        pa.Assessment.UserCityMapping.UserID,
                        Responses = pa.Responses
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .Select(r => (int?)r.Score ?? 0)
                            .ToList()
                    })
                    .ToListAsync();

                // ✅ Group results efficiently
                var cityPillars = pillarStats
                    .GroupJoin(activePillarAssessments,
                        p => p.PillarID,
                        pa => pa.PillarID,
                        (p, paGroup) =>
                        {
                            var userCount = paGroup.Select(x => x.UserID).Distinct().Count();
                            var scores = paGroup.SelectMany(x => x.Responses).ToList();

                            var totalAnsScore = scores.Sum();
                            var scoreCount = scores.Count;

                            var progress = scoreCount > 0 && userCount > 0
                                ? totalAnsScore * 100m / (scoreCount * 4m * userCount)
                                : 0m;

                            var detail = new CityPillarQuestionHistoryReponseDto
                            {
                                PillarID = p.PillarID,
                                PillarName = p.PillarName,
                                ImagePath = p.ImagePath,
                                IsAccess = p.IsAccess
                            };
                            if (p.IsAccess)
                            {
                                detail.Score = totalAnsScore;
                                detail.ScoreProgress = progress;
                                detail.AnsPillar = paGroup.Any() ? 1 : 0;
                                detail.TotalQuestion = p.QuestionCount * userCount;
                                detail.AnsQuestion = paGroup.Sum(pg => pg.Responses.Count);
                            }
                            return detail;
                        })
                    .ToList();

                // ✅ Aggregate city-level totals
                var payload = new GetCityQuestionHistoryReponseDto
                {
                    CityID = cityID,
                    TotalAssessment = await _context.Assessments
                        .CountAsync(a => a.IsActive && a.UpdatedAt.Year == currentYear && ucmIds.Contains(a.UserCityMappingID)),
                    Score = cityPillars.Sum(p => p.Score),
                    ScoreProgress = cityPillars.Any() ? cityPillars.Average(p => p.ScoreProgress) : 0,
                    TotalPillar = totalPillars * ucmIds.Count,
                    TotalAnsPillar = cityPillars.Sum(p => p.AnsPillar),
                    TotalQuestion = totalQuestions * ucmIds.Count,
                    AnsQuestion = cityPillars.Sum(p => p.AnsQuestion),
                    Pillars = cityPillars
                };

                return payload;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityQuestionHistory", ex);
                return new GetCityQuestionHistoryReponseDto
                {
                    CityID = 0,
                    Score = 0,
                    TotalPillar = 0,
                    TotalAnsPillar = 0,
                    TotalQuestion = 0,
                    AnsQuestion = 0,
                    TotalAssessment = 0,
                    Pillars = new List<CityPillarQuestionHistoryReponseDto>()
                };
            }
        }
        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request)
        {
            try
            {
                // ✅ Validate user early
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == request.UserId && x.Role == UserRole.CityUser);

                if (user == null)
                    return new PaginationResponse<CityResponseDto>();

                int currentYear = DateTime.UtcNow.Year;

                // ✅ Precompute all city scores in one grouped query (avoids N+1 queries)
                var cityScores = await _context.AnalyticalLayerResults
                    .Where(a => a.LastUpdated.Year == currentYear)
                    .GroupBy(a => a.CityID)
                    .Select(g => new { CityID = g.Key, Score = g.Average(a => (decimal?)a.CalValue5) ?? 0 })
                    .ToDictionaryAsync(x => x.CityID, x => x.Score);

                // ✅ Fetch cities mapped to the user
                var query =
                    from c in _context.Cities.AsNoTracking()
                    join pc in _context.PublicUserCityMappings.AsNoTracking()
                        on c.CityID equals pc.CityID
                    where !c.IsDeleted && pc.IsActive && pc.UserID == request.UserId
                    select new CityResponseDto
                    {
                        CityID = c.CityID,
                        CityName = c.CityName,
                        State = c.State,
                        Region = c.Region,
                        PostalCode = c.PostalCode,
                        Country = c.Country,
                        Image = c.Image,
                        CreatedDate = c.CreatedDate,
                        UpdatedDate = c.UpdatedDate,
                        IsActive = c.IsActive,
                        Score = 0 // placeholder, updated below
                    };

                // ✅ Apply search filter
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.ToLower();
                    query = query.Where(x =>
                        x.CityName.ToLower().Contains(search) ||
                        x.State.ToLower().Contains(search));
                }

                // ✅ Apply ordering and pagination
                var pagedResult = await query
                    .ApplyPaginationAsync(request);

                // ✅ Assign precomputed scores
                foreach (var city in pagedResult.Data)
                {
                    if (cityScores.TryGetValue(city.CityID, out var score))
                        city.Score = score;
                }
                pagedResult.Data = pagedResult.Data.OrderByDescending(x => x.Score);
                return pagedResult;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetCitiesAsync", ex);
                return new PaginationResponse<CityResponseDto>();
            }
        }


        public async Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.CityUser && !x.IsDeleted);

                if (user == null)
                    return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Failure(new[] { "Invalid request" });

                var date = DateTime.Now;

                // Get total pillars and questions
                var pillarStats = await _context.Pillars
                    .Select(p => new { p.PillarID, QuestionsCount = p.Questions.Count })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                // Determine allowed pillars based on tier
                var pillarPredicate = user.Tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 15
                };

                var allowedPillarIds = pillarStats
                    .Where(p => p.PillarID < pillarPredicate)
                    .Select(p => p.PillarID)
                    .ToHashSet();

                // Query data with joins and projection
                var citySubmission = await (
                    from uc in _context.UserCityMappings
                    where !uc.IsDeleted
                    join c in _context.Cities.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.CityID equals c.CityID
                    join a in _context.Assessments.Where(a => a.IsActive && a.UpdatedAt.Year == date.Year)
                        on uc.UserCityMappingID equals a.UserCityMappingID into assessments
                    from a in assessments.DefaultIfEmpty()
                    select new
                    {
                        c.CityID,
                        c.CityName,
                        AssessmentID = (int?)a.AssessmentID,
                        PillarAssessments = a.PillarAssessments.Where(pa => allowedPillarIds.Contains(pa.PillarID)),
                        Responses = a.PillarAssessments
                                      .Where(pa => allowedPillarIds.Contains(pa.PillarID))
                                      .SelectMany(pa => pa.Responses)
                    }
                )
                .AsNoTracking()
                .ToListAsync();

                // Group by city and calculate metrics
                var result = citySubmission
                    .GroupBy(g => new { g.CityID, g.CityName })
                    .Select(g =>
                    {
                        var allPillars = g.SelectMany(x => x.PillarAssessments).ToList();
                        var aspIds = allPillars.Select(x => x.PillarAssessmentID).ToHashSet();
                        var allResponses = g.SelectMany(x => x.Responses).Where(r => aspIds.Contains(r.PillarAssessmentID)).ToList();

                        var scoreList = allResponses
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .Select(r => (int?)r.Score ?? 0);

                        int userCityMappingCount = g.Count();

                        return new GetCitiesSubmitionHistoryReponseDto
                        {
                            CityID = g.Key.CityID,
                            CityName = g.Key.CityName,
                            TotalAssessment = g.Select(x => x.AssessmentID).Where(id => id.HasValue).Distinct().Count(),
                            Score = allResponses.Sum(r => (int?)r.Score ?? 0),
                            TotalPillar = totalPillars * userCityMappingCount,
                            TotalAnsPillar = allPillars.Count,
                            TotalQuestion = totalQuestions * userCityMappingCount,
                            AnsQuestion = allResponses.Count,
                            ScoreProgress = scoreList.Any() ? (scoreList.Sum() * 100m) / (scoreList.Count() * 4) : 0m
                        };
                    }).ToList();

                return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Success(result, new List<string> { "Get Cities history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCitiesProgressByUserId", ex);
                return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Failure(new[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<CityDetailsDto>> GetCityDetails(UserCityRequstDto userCityRequstDto)
        {
            try
            {
                var cityId = userCityRequstDto.CityID;
                var date = userCityRequstDto.UpdatedAt;

                // 1. Validate city
                var city = await _context.Cities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CityID == cityId && x.IsActive && !x.IsDeleted);

                if (city == null)
                    return ResultResponseDto<CityDetailsDto>.Failure(new[] { "Invalid city ID" });

                // 2. Get all assessments for this city (for the given year)
                var assessments = await (
                    from a in _context.Assessments
                        .Include(x => x.PillarAssessments)
                            .ThenInclude(p => p.Responses)
                    join uc in _context.UserCityMappings.Where(x => !x.IsDeleted)
                        on a.UserCityMappingID equals uc.UserCityMappingID
                    where uc.CityID == cityId && a.IsActive && a.UpdatedAt.Year == date.Year
                    select a
                ).ToListAsync();

                var cityDetails = new CityDetailsDto
                {
                    CityID = cityId,
                    TotalEvaluation = assessments.Count
                };

                // 3. Get all pillars (ensures even pillars without responses appear)
                var allPillars = await _context.Pillars
                    .Include(x=>x.Questions)
                    .ThenInclude(x=>x.QuestionOptions)
                    .AsNoTracking()
                    .ToListAsync();

                // 4. Flatten all PillarAssessments (from assessments)
                var pillarAssessments = assessments
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => pa != null)
                    .ToList();

                // 5. Flatten all responses
                var allResponses = pillarAssessments
                    .SelectMany(p => p.Responses)
                    .Where(r => r != null)
                    .ToList();

                var accessPillarsIds = await _context.CityUserPillarMappings.Where(x => x.UserID == userCityRequstDto.UserID).Select(x => x.PillarID).ToListAsync();

                var validResponses = allResponses
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                    .ToList();

                cityDetails.TotalPillar = allPillars.Count * assessments.Count;
                cityDetails.TotalAnsPillar = pillarAssessments.Count(p => p.Responses.Any());
                cityDetails.TotalQuestion = allPillars.SelectMany(x=>x.Questions).Count() * assessments.Count;
                cityDetails.AnsQuestion = validResponses.Count;

                cityDetails.TotalScore = validResponses.Sum(r => (decimal?)r.Score ?? 0);
                cityDetails.ScoreProgress = validResponses.Any()
                    ? (cityDetails.TotalScore * 100M) / (validResponses.Count * 4M * assessments.Count)
                    : 0M;

                // 6. Compute pillar-level data (include pillars without assessments)
                cityDetails.Pillars = allPillars
                    .Select(p =>
                    {
                        var paForPillar = pillarAssessments.Where(x => x.PillarID == p.PillarID).ToList();
                        var responses = paForPillar.SelectMany(x => x.Responses)
                            .Where(r => r != null && r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .ToList();

                        var naUnknownIds = responses.Where(x => !x.Score.HasValue).Select(x => x.QuestionOptionID);

                        var naUnknownOptions = p.Questions.SelectMany(x=>x.QuestionOptions).Where(x=> naUnknownIds.Contains(x.OptionID));

                        var totalQuestions = p.Questions.Count() * assessments.Count;
                        var answeredQuestions = responses.Count;
                        var totalScore = responses.Sum(r => (decimal?)r.Score ?? 0);

                        var scoreProgress = answeredQuestions > 0
                            ? (totalScore * 100M) / (answeredQuestions * 4M * assessments.Count)
                            : 0M;


                        var isAccess = accessPillarsIds.Any(x=>p.PillarID == x);

                        if (isAccess)
                        {
                            return new CityPillarDetailsDto
                            {
                                PillarID = p.PillarID,
                                PillarName = p.PillarName,
                                TotalPillar = paForPillar.Count,
                                DisplayOrder = p.DisplayOrder,
                                TotalAnsPillar = paForPillar.Count(x => x.Responses.Any()),
                                TotalQuestion = totalQuestions,
                                AnsQuestion = answeredQuestions,
                                TotalScore = totalScore,
                                ScoreProgress = scoreProgress,
                                AvgHighScore = responses.Any() ? responses.Max(r => (decimal?)r.Score ?? 0) : 0,
                                AvgLowerScore = responses.Any() ? responses.Min(r => (decimal?)r.Score ?? 0) : 0,
                                TotalNA = naUnknownOptions.Where(x => x.OptionText.Contains("N/A")).Count(),
                                TotalUnKnown = naUnknownOptions.Where(x => x.OptionText.Contains("Unknown")).Count(),
                                IsAccess = isAccess
                            };
                        }
                        else
                        {
                            return new CityPillarDetailsDto
                            {
                                PillarID = p.PillarID,
                                PillarName = p.PillarName,
                                DisplayOrder = p.DisplayOrder
                            };
                        }
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                // 7. Compute high/low pillar scores for summary
                var pillarScores = cityDetails.Pillars.Select(p => p.TotalScore).ToList();
                if (pillarScores.Any())
                {
                    cityDetails.AvgHighScore = pillarScores.Max();
                    cityDetails.AvgLowerScore = pillarScores.Min();
                }

                return ResultResponseDto<CityDetailsDto>.Success(
                    cityDetails,
                    new List<string> { "Get city details successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCityDetails", ex);
                return ResultResponseDto<CityDetailsDto>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<CityPillarQuestionDetailsDto>>> GetCityPillarDetails(UserCityGetPillarInfoRequstDto userCityRequstDto)
        {
            try
            {
                var cityId = userCityRequstDto.CityID;
                var pillarId = userCityRequstDto.PillarID;
                var date = userCityRequstDto.UpdatedAt;

                // 1. Validate city and pillar
                var city = await _context.Cities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CityID == cityId && x.IsActive && !x.IsDeleted);

                if (city == null)
                    return ResultResponseDto<List<CityPillarQuestionDetailsDto>>.Failure(new[] { "Invalid city ID" });

                var pillar = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == pillarId);

                if (pillar == null)
                    return ResultResponseDto<List<CityPillarQuestionDetailsDto>>.Failure(new[] { "Invalid pillar ID" });

                // 2. Get all assessments for this city in the given year
                var assessments = await (
                    from a in _context.Assessments
                        .Include(x => x.PillarAssessments)
                            .ThenInclude(pa => pa.Responses)
                                .ThenInclude(r => r.Question)
                    join uc in _context.UserCityMappings.Where(x => !x.IsDeleted)
                        on a.UserCityMappingID equals uc.UserCityMappingID
                    where uc.CityID == cityId && a.IsActive && a.UpdatedAt.Year == date.Year
                    select a
                ).ToListAsync();

                if (!assessments.Any())
                    return ResultResponseDto<List<CityPillarQuestionDetailsDto>>.Failure(new[] { "No assessments found for the given city/year." });

                // 3. Flatten pillar assessments for this pillar
                var pillarAssessments = assessments
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => pa.PillarID == pillarId)
                    .ToList();

                // 4. Flatten all responses for this pillar
                var allResponses = pillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r != null)
                    .ToList();

                var validResponses = allResponses
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                    .ToList();

                // 5. Generate question-level metrics
                var result = pillar.Questions
                    .OrderBy(x=>x.DisplayOrder)
                    .Select(q =>
                    {
                        var qResponses = validResponses.Where(r => r.QuestionID == q.QuestionID).ToList();
                        var totalQuestions = 1 * assessments.Count; // Each item represents one question
                        var answeredQuestions = qResponses.Count;
                        var totalScore = qResponses.Sum(r => (decimal?)r.Score ?? 0);

                        // Compute "Unknown" and "N/A" counts
                        var naUnknownIds = allResponses
                            .Where(r => r.QuestionID == q.QuestionID && !r.Score.HasValue)
                            .Select(r => r.QuestionOptionID);

                        var naUnknownOptions = q.QuestionOptions
                            .Where(opt => naUnknownIds.Contains(opt.OptionID))
                            .ToList();

                        var totalNA = naUnknownOptions.Count(opt => opt.OptionText.Contains("N/A"));
                        var totalUnknown = naUnknownOptions.Count(opt => opt.OptionText.Contains("Unknown"));

                        var scoreProgress = answeredQuestions > 0
                            ? (totalScore * 100M) / (answeredQuestions * 4M * assessments.Count)
                            : 0M;

                        return new CityPillarQuestionDetailsDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            TotalQuestion = totalQuestions,
                            AnsQuestion = answeredQuestions,
                            TotalScore = totalScore,
                            ScoreProgress = scoreProgress,
                            AvgHighScore = qResponses.Any() ? qResponses.Max(r => (decimal?)r.Score ?? 0) : 0,
                            AvgLowerScore = qResponses.Any() ? qResponses.Min(r => (decimal?)r.Score ?? 0) : 0,
                            TotalNA = totalNA,
                            TotalUnKnown = totalUnknown
                        };
                    })
                .ToList();

                return ResultResponseDto<List<CityPillarQuestionDetailsDto>>.Success(
                    result,
                    new List<string> { "Get city pillar question details successfully" }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCityPillarDetails", ex);
                return ResultResponseDto<List<CityPillarQuestionDetailsDto>>.Failure(new[] { "There is an error, please try later" });
            }
        }
        public async Task<ResultResponseDto<List<PartnerCityResponseDto>>> GetCityUserCities(int userID)
        {
            try
            {
                int currentYear = DateTime.UtcNow.Year;

                // Precompute all scores in a single grouped query (avoids N+1 subqueries)
                var scoreDict = await _context.AnalyticalLayerResults
                    .Where(a => a.LastUpdated.Year == currentYear)
                    .GroupBy(a => a.CityID)
                    .Select(g => new { CityID = g.Key, Score = g.Average(a => (decimal?)a.CalValue5) ?? 0 })
                    .ToDictionaryAsync(x => x.CityID, x => x.Score);

                // Convert scores to dictionary for quick lookup
                

                // Fetch cities assigned to the user
                var result = await _context.PublicUserCityMappings
                    .AsNoTracking()
                    .Where(m => m.UserID == userID && m.IsActive && m.City != null && !m.City.IsDeleted)
                    .Select(m => new PartnerCityResponseDto
                    {
                        CityID = m.City.CityID,
                        CityName = m.City.CityName,
                        State = m.City.State,
                        Region = m.City.Region,
                        PostalCode = m.City.PostalCode,
                        Country = m.City.Country,
                        Image = m.City.Image,
                        Score = 0 // placeholder
                    })
                    .OrderBy(x => x.CityName)
                    .ToListAsync();

                // Attach scores efficiently
                foreach (var city in result)
                {
                    if (scoreDict.TryGetValue(city.CityID, out var score))
                        city.Score = Math.Round(score,2);
                }

                return ResultResponseDto<List<PartnerCityResponseDto>>.Success(
                    result,
                    new[] { "Fetched all assigned cities successfully." }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityUserCities", ex);
                return ResultResponseDto<List<PartnerCityResponseDto>>.Failure(
                    new[] { "There was an error. Please try again later." }
                );
            }
        }

        public async Task<ResultResponseDto<string>> AddCityUserKpisCityAndPillar(AddCityUserKpisCityAndPillar payload, int userId, string tierName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tierName))
                    return ResultResponseDto<string>.Failure(new[] { "Access tier information is missing. Please log in again." });

                if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var tier))
                    return ResultResponseDto<string>.Failure(new[] { "Invalid access tier. Please contact support." });

                var tierLimits = tier switch
                {
                    TieredAccessPlan.Basic => new { Min = 2, Max = 3, Name = "Basic" },
                    TieredAccessPlan.Standard => new { Min = 5, Max = 7, Name = "Standard" },
                    TieredAccessPlan.Premium => new { Min = 0, Max = int.MaxValue, Name = "Premium" },
                    _ => new { Min = 0, Max = 0, Name = "Unknown" }
                };

                if (tier != TieredAccessPlan.Premium)
                {
                    bool isValid =
                        payload.Cities.Count >= tierLimits.Min && payload.Cities.Count <= tierLimits.Max &&
                        payload.Pillars.Count >= tierLimits.Min && payload.Pillars.Count <= tierLimits.Max &&
                        payload.Kpis.Count >= tierLimits.Min && payload.Kpis.Count <= tierLimits.Max;

                    if (!isValid)
                    {
                        return ResultResponseDto<string>.Failure(new[]
                        {
                            $"Your {tierLimits.Name} plan allows between {tierLimits.Min} and {tierLimits.Max} selections per category (City, Pillar, and KPI). Please adjust your selections accordingly."
                        });
                    }
                }

                //  Remove existing mappings
                var existingCities = await _context.PublicUserCityMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                var existingPillars = await _context.CityUserPillarMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                var existingKpis = await _context.CityUserKpiMappings
                    .Where(m => m.UserID == userId)
                    .ToListAsync();

                _context.PublicUserCityMappings.RemoveRange(existingCities);
                _context.CityUserPillarMappings.RemoveRange(existingPillars);
                _context.CityUserKpiMappings.RemoveRange(existingKpis);

                var utcNow = DateTime.UtcNow;

                var newCityMappings = payload.Cities.Select(cityId => new PublicUserCityMapping
                {
                    CityID = cityId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                var newPillarMappings = payload.Pillars.Select(pillarId => new CityUserPillarMapping
                {
                    PillarID = pillarId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                var newKpiMappings = payload.Kpis.Select(kpiId => new CityUserKpiMapping
                {
                    LayerID = kpiId,
                    UserID = userId,
                    IsActive = true,
                    UpdatedAt = utcNow
                });

                await _context.PublicUserCityMappings.AddRangeAsync(newCityMappings);
                await _context.CityUserPillarMappings.AddRangeAsync(newPillarMappings);
                await _context.CityUserKpiMappings.AddRangeAsync(newKpiMappings);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Your preferences have been saved successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in AddCityUserKpisCityAndPillar", ex);
                return ResultResponseDto<string>.Failure(new[]
                {
                    "Something went wrong while saving your selections. Please try again later."
                });
            }
        }
        public async Task<ResultResponseDto<List<AnalyticalLayer>>> GetCityUserKpi(int userId, string tierName)
        {
            try
            {
                // Get valid KPI IDs for the user (only non-deleted mappings)
                var validKpiIds = await _context.CityUserKpiMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.LayerID)
                    .ToListAsync();

                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "you don't have kpi access." });
                }

                // Fetch Analytical Layers that match the user's KPI access
                var result = await _context.AnalyticalLayers
                    .Where(ar => !ar.IsDeleted && validKpiIds.Contains(ar.LayerID))
                    .ToListAsync();

                return ResultResponseDto<List<AnalyticalLayer>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityUserKpi", ex);
                return ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "An error occurred while fetching user KPIs." });
            }
        }

        public async Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, string tierName)
        {
            try
            {
                // Step 1: Get valid KPI IDs for this user
                var validKpiIds = await _context.CityUserKpiMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.LayerID)
                    .ToListAsync();

                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "You don't have KPI access." });
                }

                // Step 2: Get all selected cities (even if no analytical data)
                var selectedCities = await _context.Cities
                    .Where(x => c.Cities.Contains(x.CityID))
                    .Select(x => new { x.CityID, x.CityName })
                    .ToListAsync();

                if (!selectedCities.Any())
                {
                    return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "No valid cities found." });
                }

                // Step 3: Fetch analytical layer results for selected cities
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => c.Cities.Contains(x.CityID) &&
                                x.LastUpdated.Year == c.UpdatedAt.Year &&
                                validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.CityID,
                        ar.LayerID,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.CalValue5
                    })
                    .ToListAsync();

                // Step 4: Get all distinct layers
                var allLayers = analyticalResults
                    .Select(x => new { x.LayerID, x.LayerCode, x.LayerName })
                    .Distinct()
                    .OrderBy(x => x.LayerName)
                    .ToList();

                // Step 5: Prepare response DTO
                var response = new CompareCityResponseDto
                {
                    Categories = new List<string>(),
                    Series = new List<ChartSeriesDto>(),
                    TableData = new List<ChartTableRowDto>()
                };

                // Initialize chart series for each city
                foreach (var city in selectedCities)
                {
                    response.Series.Add(new ChartSeriesDto
                    {
                        Name = city.CityName,
                        Data = new List<decimal>()
                    });
                }

                // Add Peer City Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer City Score",
                    Data = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each city (0 if missing)
                    var values = new Dictionary<int, decimal>();

                    foreach (var city in selectedCities)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.CityID == city.CityID && r.LayerID == layer.LayerID)
                            ?.CalValue5 ?? 0;

                        var roundedValue = Math.Round(value, 2);
                        values[city.CityID] = roundedValue;

                        // Add to series
                        var citySeries = response.Series.First(s => s.Name == city.CityName);
                        citySeries.Data.Add(roundedValue);
                    }

                    // ✅ Calculate Peer City Score (average of all cities for this layer)
                    var peerCityScore = values.Values.Any() ? Math.Round(values.Values.Average(), 2) : 0;
                    peerSeries.Data.Add(peerCityScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        CityValues = selectedCities.Select(c => new CityValueDto
                        {
                            CityID = c.CityID,
                            CityName = c.CityName,
                            Value = values[c.CityID]
                        }).ToList(),
                        PeerCityScore = peerCityScore // You can rename property if needed
                    });
                }

                // Append Peer City Score series
                response.Series.Add(peerSeries);

                return ResultResponseDto<CompareCityResponseDto>.Success(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in CompareCities", ex);
                return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "An error occurred while comparing cities." });
            }
        }
    }
}
