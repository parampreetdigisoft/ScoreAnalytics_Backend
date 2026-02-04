
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;

using DocumentFormat.OpenXml.Drawing.Charts;

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
        public async Task<ResultResponseDto<CityHistoryDto>> GetCityHistory(int userId, TieredAccessPlan tier)
        {
            try
            {
                int year = DateTime.UtcNow.Year;

                int allowedPillars = tier switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                var accessibleCityIds = await _context.PublicUserCityMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId && x.IsActive)
                    .Select(x => x.CityID)
                    .ToListAsync();

                if (!accessibleCityIds.Any())
                {
                    return ResultResponseDto<CityHistoryDto>.Failure(new List<string> { "No cities available for user" });
                }

                var verifiedCityScores = await _context.AICityScores
                    .AsNoTracking()
                    .Where(x =>
                        accessibleCityIds.Contains(x.CityID) &&
                        x.Year == year &&
                        x.IsVerified)
                    .Select(x => x.AIProgress)
                    .ToListAsync();

                var cityHistory = new CityHistoryDto
                {
                    TotalCity = accessibleCityIds.Count,
                    TotalAccessCity = accessibleCityIds.Count,
                    ActiveCity = verifiedCityScores.Count
                };

                if (verifiedCityScores.Any())
                {
                    cityHistory.AvgHighScore = verifiedCityScores.Max() ?? 0;
                    cityHistory.AvgLowerScore = verifiedCityScores.Min() ?? 0;
                    cityHistory.OverallVitalityScore = verifiedCityScores.Average() ?? 0;
                }

                return ResultResponseDto<CityHistoryDto>.Success(cityHistory,new List<string> { "Get history successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in GetCityHistory", ex);
                return ResultResponseDto<CityHistoryDto>.Failure(new[] { "There is an error, please try later" });
            }
        }


        public async Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto request)
        {
            try
            {
                int userId = request.UserID;
                int cityId = request.CityID;
                int year = request.UpdatedAt.Year;

                int allowedPillars = request.Tiered switch
                {
                    Enums.TieredAccessPlan.Basic => 4,
                    Enums.TieredAccessPlan.Standard => 8,
                    Enums.TieredAccessPlan.Premium => 15,
                    _ => 0
                };

                // 🔹 Fetch accessible pillar IDs
                var accessiblePillarIds = await _context.CityUserPillarMappings
                    .AsNoTracking()
                    .Where(x => x.UserID == userId)
                    .OrderBy(x => x.PillarID)
                    .Select(x => x.PillarID)
                    .Take(allowedPillars)
                    .ToListAsync();

                var accessiblePillarSet = accessiblePillarIds.ToHashSet();

                // 🔹 Fetch city score once
                var cityScore = await _context.AICityScores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CityID == cityId && x.Year == year);

                // 🔹 Fetch pillars
                var pillars = await _context.Pillars
                    .AsNoTracking()
                    .OrderBy(x => x.DisplayOrder)
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.ImagePath,
                        p.DisplayOrder
                    })
                    .ToListAsync();

                // 🔹 Fetch pillar scores and map for O(1) lookup
                var pillarScoreMap = await _context.AIPillarScores
                    .AsNoTracking()
                    .Where(x => x.CityID == cityId && x.Year == year)
                    .ToDictionaryAsync(x => x.PillarID);

                // 🔹 Build DTOs
                var pillarDtos = pillars
                    .Select(p =>
                    {
                        bool isAccess = accessiblePillarSet.Contains(p.PillarID);
                        pillarScoreMap.TryGetValue(p.PillarID, out var aiScore);

                        return new CityPillarQuestionHistoryReponseDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            ImagePath = p.ImagePath,
                            IsAccess = isAccess,
                            Score = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            ScoreProgress = isAccess ? aiScore?.AIProgress ?? 0 : 0,
                            DisplayOrder = p.DisplayOrder // optional if DTO supports it
                        };
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                return new GetCityQuestionHistoryReponseDto
                {
                    CityID = cityId,
                    TotalAssessment = pillarScoreMap.Count,
                    Score = cityScore?.AIProgress ?? 0,
                    ScoreProgress = cityScore?.AIProgress ?? 0,
                    Pillars = pillarDtos
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetCityQuestionHistory (Optimized)", ex);
                return new GetCityQuestionHistoryReponseDto();
            }
        }


        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request)
        {
            try
            {

                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var cityScores = from ac in _context.AICityScores
                                 .Where(x => x.UpdatedAt >= startDate && x.UpdatedAt < endDate && x.IsVerified && x.Year == year)
                                 join pc in _context.PublicUserCityMappings on ac.CityID equals pc.CityID
                                 group ac by ac.CityID into g
                                 select new
                                 {
                                     CityID = g.Key,
                                     Score = g.Average(x => (decimal?)x.AIProgress) ?? 0
                                 };


                // ✅ Fetch cities mapped to the user
                var query =
                    from c in _context.Cities.AsNoTracking()
                    join pc in _context.PublicUserCityMappings.AsNoTracking()
                        on c.CityID equals pc.CityID

                    join s in cityScores on pc.CityID equals s.CityID into scores
                    from s in scores.DefaultIfEmpty() // Left join
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
                        Score = s.Score
                    };

                // ✅ Apply search filter
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string search = request.SearchText.ToLower();
                    query = query.Where(x => x.CityName.ToLower().Contains(search) || x.State.ToLower().Contains(search));
                }

                // ✅ Apply ordering and pagination
                var pagedResult = await query.ApplyPaginationAsync(request);


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
                var userId = userCityRequstDto.UserID;
                var year = userCityRequstDto.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Validate city
                var city = await _context.Cities
                    .AsNoTracking()
                    .Where(x => x.CityID == cityId && x.IsActive && !x.IsDeleted)
                    .Select(x => new { x.CityID })
                    .FirstOrDefaultAsync();

                if (city == null)
                    return ResultResponseDto<CityDetailsDto>.Failure(new[] { "Invalid city ID" });

                // Get user access pillars
                var accessPillarIds = await _context.CityUserPillarMappings
                    .Where(x => x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Get all active pillars and questions
                var allPillars = await _context.Pillars
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.DisplayOrder,
                        Questions = p.Questions.Select(q => new
                        {
                            q.QuestionID,
                            Options = q.QuestionOptions.Select(o => new { o.OptionID, o.OptionText })
                        }).ToList()
                    })
                    .ToListAsync();

                // Preload all assessments + pillar assessments + responses (flattened projection)
                var assessmentsData = await (
                    from a in _context.Assessments
                    join uc in _context.UserCityMappings on a.UserCityMappingID equals uc.UserCityMappingID
                    where uc.CityID == cityId &&
                          a.IsActive &&
                          a.UpdatedAt >= startDate &&
                          a.UpdatedAt < endDate &&
                          !uc.IsDeleted
                    select new
                    {
                        a.AssessmentID,
                        Pillars = a.PillarAssessments.Select(pa => new
                        {
                            pa.PillarID,
                            Responses = pa.Responses.Select(r => new { r.Score, r.QuestionOptionID })
                        })
                    }
                ).AsNoTracking().ToListAsync();

                var totalAssessments = assessmentsData.Count;

                if (totalAssessments == 0)
                {
                    return ResultResponseDto<CityDetailsDto>.Success(
                        new CityDetailsDto
                        {
                            CityID = cityId,
                            TotalEvaluation = 0,
                            TotalPillar = allPillars.Count,
                            TotalAnsPillar = 0,
                            TotalQuestion = allPillars.SelectMany(x => x.Questions).Count(),
                            AnsQuestion = 0,
                            ScoreProgress = 0,
                            Pillars = new List<CityPillarDetailsDto>()
                        },
                        new List<string> { "No assessments found for this city." }
                    );
                }

                // Flatten all pillar assessments and responses
                var allResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                    .ToList();

                // Compute City level stats
                var totalPillars = allPillars.Count * totalAssessments;
                var totalQuestions = allPillars.Sum(p => p.Questions.Count) * totalAssessments;
                var answeredQuestions = allResponses.Count;
                var totalScore = allResponses.Sum(r => (int?)r.Score ?? 0);
                var scoreProgress = answeredQuestions > 0
                    ? (totalScore * 100M) / (answeredQuestions * 4M)
                    : 0M;

                // Group responses by pillar
                var groupedResponses = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four).ToList());

                var naUnknownGroup = assessmentsData
                    .SelectMany(a => a.Pillars)
                    .GroupBy(p => p.PillarID)
                    .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Responses).Where(r => !r.Score.HasValue).ToList());


                // Build pillar details
                var pillarDetails = allPillars
                    .Select(p =>
                    {
                        var isAccess = accessPillarIds.Contains(p.PillarID);

                        var payload = new CityPillarDetailsDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            DisplayOrder = p.DisplayOrder,
                            IsAccess = isAccess
                        };

                        if (isAccess)
                        {
                            groupedResponses.TryGetValue(p.PillarID, out var responses);

                            var validResponses = responses?.ToList<dynamic>() ?? new List<dynamic>();

                            var totalQuestionsForPillar = p.Questions.Count * totalAssessments;
                            var answered = validResponses.Count;
                            var totalPillarScore = validResponses.Sum(r => (int?)r.Score ?? 0);
                            var scorePct = answered > 0 ? (totalPillarScore * 100M) / (answered * 4M) : 0M;


                            naUnknownGroup.TryGetValue(p.PillarID, out var naUnknownRes);

                            var naUnknownResponse = naUnknownRes?.ToList<dynamic>() ?? new List<dynamic>();

                            var naUnknownOptionIds = naUnknownResponse.Select(r => r.QuestionOptionID).ToList();

                            var naUnknownOptions = p.Questions
                                .SelectMany(q => q.Options)
                                .Where(o => naUnknownOptionIds.Contains(o.OptionID))
                                .ToList();

                            payload.TotalQuestion = totalQuestionsForPillar;
                            payload.AnsQuestion = answered;
                            payload.TotalScore = totalPillarScore;
                            payload.ScoreProgress = scorePct;
                            payload.AvgHighScore = validResponses.Any() ? validResponses.Max(r => (int?)r.Score ?? 0) : 0;
                            payload.AvgLowerScore = validResponses.Any() ? validResponses.Min(r => (int?)r.Score ?? 0) : 0;
                            payload.TotalNA = naUnknownOptions.Count(o => o.OptionText.Contains("N/A"));
                            payload.TotalUnKnown = naUnknownOptions.Count(o => o.OptionText.Contains("Unknown"));
                        }
                        return payload;
                    })
                    .OrderByDescending(x => x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();

                var cityDetails = new CityDetailsDto
                {
                    CityID = cityId,
                    TotalEvaluation = totalAssessments,
                    TotalPillar = totalPillars,
                    TotalAnsPillar = pillarDetails.Count(p => p.AnsQuestion > 0),
                    TotalQuestion = totalQuestions,
                    AnsQuestion = answeredQuestions,
                    TotalScore = totalScore,
                    ScoreProgress = scoreProgress,
                    AvgHighScore = pillarDetails.Any() ? pillarDetails.Max(p => p.TotalScore) : 0,
                    AvgLowerScore = pillarDetails.Any() ? pillarDetails.Min(p => p.TotalScore) : 0,
                    Pillars = pillarDetails
                };

                return ResultResponseDto<CityDetailsDto>.Success(cityDetails, new[] { "Get city details successfully" });
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
                    .OrderBy(x => x.DisplayOrder)
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
                int year = DateTime.UtcNow.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Step 1️⃣: Fetch city score averages as a dictionary
                var cityScoresDict = await _context.AICityScores
                    .Where(ar => ar.UpdatedAt >= startDate && ar.UpdatedAt < endDate && ar.IsVerified && ar.Year == year)
                    .GroupBy(ar => ar.CityID)
                    .Select(g => new
                    {
                        CityID = g.Key,
                        Score = g.Average(x => (decimal?)x.EvaluatorProgress) ?? 0,
                        AiScore = g.Average(x => (decimal?)x.AIProgress) ?? 0
                    })
                    .ToDictionaryAsync(x => x.CityID, x => new { x.Score, x.AiScore });

                // Step 2️⃣: Fetch cities assigned to the user
                var cities = await _context.PublicUserCityMappings
                    .Where(x => x.IsActive && x.City != null && !x.City.IsDeleted && x.UserID == userID)
                    .Select(c => new PartnerCityResponseDto
                    {
                        CityID = c.City.CityID,
                        CityName = c.City.CityName,
                        State = c.City.State,
                        PostalCode = c.City.PostalCode,
                        Region = c.City.Region,
                        Country = c.City.Country,
                        Image = c.City.Image
                    })
                    .AsNoTracking()
                    .ToListAsync();

                // Step 3️⃣: Map score from dictionary (safe fallback to 0)
                foreach (var city in cities)
                {
                    if (cityScoresDict.TryGetValue(city.CityID, out var score))
                    {
                        city.Score = score.AiScore;
                        city.AiScore = score.AiScore;
                    }
                }

                // Step 4️⃣: Sort by score descending
                var result = cities.OrderByDescending(x => x.Score).ToList();

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
                    return ResultResponseDto<string>.Failure(new[] { "Invalid tier access. Please contact support team." });

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
        public async Task<ResultResponseDto<List<GetAllKpisResponseDto>>> GetCityUserKpi(int userId, string tierName)
        {
            try
            {
                var validPillarIds = await _context.CityUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID)
                    .ToListAsync();

                // Step 1: Get valid KPI IDs for this user
                var validKpiIds = await _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct()
                    .ToListAsync();

                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "you don't have kpi access." });
                }

                // Fetch Analytical Layers that match the user's KPI access
                var result = await _context.AnalyticalLayers
                    .Where(ar => !ar.IsDeleted && validKpiIds.Contains(ar.LayerID))
                    .Select(x=>new GetAllKpisResponseDto
                    {
                        LayerID = x.LayerID,
                        LayerCode = x.LayerCode,
                        LayerName = x.LayerName
                    })
                    .ToListAsync();

                return ResultResponseDto<List<GetAllKpisResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityUserKpi", ex);
                return ResultResponseDto<List<GetAllKpisResponseDto>>.Failure(new List<string> { "An error occurred while fetching user KPIs." });
            }
        }

        public async Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, string tierName)
        {
            try
            {
                var year = c.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var validKpiIds = new List<int>();
                if (c.Kpis.Count == 0)
                {
                    var validPillarIds = _context.CityUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID);

                    // Step 1: Get valid KPI IDs for this user
                    var query = _context.AnalyticalLayerPillarMappings
                        .Where(x => validPillarIds.Contains(x.PillarID))
                        .Select(x => x.LayerID)
                        .Distinct();

                    var res = await query.ApplyPaginationAsync(c);
                    validKpiIds = res.Data.ToList();
                }
                else
                {
                    validKpiIds = c.Kpis;
                }


                if (!validKpiIds.Any())
                {
                    return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "You don't have KPI access." });
                }

                // Step 2: Get all selected cities (even if no analytical data)
                var selectedCities = await _context.PublicUserCityMappings
                    .Include(x=>x.City)
                    .Where(x => c.Cities.Contains(x.CityID) && x.UserID== userId && x.IsActive && x.City != null && x.City.IsActive)
                    .Select(x => new { x.City.CityID, x.City.CityName })
                    .ToListAsync();

                if (!selectedCities.Any())
                {
                    return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "No valid cities found." });
                }

                // Step 3: Fetch analytical layer results for selected cities
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => c.Cities.Contains(x.CityID) &&
                                ((x.LastUpdated >= startDate && x.LastUpdated < endDate) || (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate))
                                && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.CityID,
                        ar.LayerID,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.CalValue5,
                        ar.AiCalValue5
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
                        AiData = new List<decimal>()
                    });
                }

                // Add Peer City Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer City Score",
                    AiData = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each city (0 if missing)
                    var values = new Dictionary<int, List<decimal>>();

                    foreach (var city in selectedCities)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.CityID == city.CityID && r.LayerID == layer.LayerID);

                        var evaluatedValue = Math.Round(value?.CalValue5 ?? 0, 2);
                        var aiValue = Math.Round(value?.AiCalValue5 ?? 0, 2);
                        values[city.CityID] = new List<decimal> { evaluatedValue, aiValue };

                        //// Add to series
                        var citySeries = response.Series.First(s => s.Name == city.CityName);

                        citySeries.AiData.Add(aiValue);
                    }

                    var aiPeerCityScore = values.Values.Any() ? Math.Round(values.Values.Select(x=>x.Last()).Average(), 2) : 0;
                    peerSeries.AiData.Add(aiPeerCityScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerID=layer.LayerID,
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        CityValues = selectedCities.Select(c => new CityValueDto
                        {
                            CityID = c.CityID,
                            CityName = c.CityName,
                            AiValue =  values[c.CityID].Last()
                        }).ToList(),
                        PeerCityScore = aiPeerCityScore // You can rename property if needed
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
        public async Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(AiCityPillarRequestDto request, int userID, string tierName)
        {
            try
            {
                var currentYear = request.Year;
                var firstDate = new DateTime(currentYear, 1, 1);

                // 1. Check if city is finalized for this user (EXISTS instead of JOIN)
                var isCityFinalized = await _context.PublicUserCityMappings
                    .AnyAsync(pum =>
                        pum.UserID == userID &&
                        pum.CityID == request.CityID &&
                        _context.AICityScores.Any(ac =>
                            ac.CityID == request.CityID && ac.IsVerified && ac.Year == currentYear));

                if (!isCityFinalized)
                {
                    return ResultResponseDto<AiCityPillarReponseDto>.Failure(new[] { "City is under review process try after some time", });
                }

                var res = await _context.AIPillarScores
                    .Where(x => x.CityID == request.CityID && x.UpdatedAt >= firstDate && x.Year == currentYear) 
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds =  await _context.CityUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();
                
                var pillars = await _context.Pillars.ToListAsync();

                var result = pillars
                .GroupJoin(
                    res,
                    p => p.PillarID,
                    s => s.PillarID,
                    (pillar, scores) => new { pillar, score = scores.FirstOrDefault() }
                )
                .Select(x =>
                {
                    var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.pillar.PillarID);

                    var r = new AiCityPillarReponse
                    {
                        PillarScoreID = x.score?.PillarScoreID ?? 0,
                        CityID = x.score?.CityID ?? request.CityID,
                        CityName = x.score?.City?.CityName ?? "",
                        State = x.score?.City?.State ?? "",
                        Country = x.score?.City?.Country ?? "",
                        PillarID = x.pillar.PillarID,
                        PillarName = x.pillar.PillarName,
                        DisplayOrder = x.pillar.DisplayOrder,
                        ImagePath = x.pillar.ImagePath,
                        IsAccess = isAccess
                    };

                    if (isAccess && x.score != null)
                    {
                        r.AIDataYear = x.score.Year;
                        r.AIScore = x.score.AIScore;
                        r.AIProgress = x.score.AIProgress;
                        r.EvidenceSummary = x.score.EvidenceSummary;
                        r.RedFlags = x.score.RedFlags;
                        r.GeographicEquityNote = x.score.GeographicEquityNote;
                        r.InstitutionalAssessment = x.score.InstitutionalAssessment;
                        r.DataGapAnalysis = x.score.DataGapAnalysis;
                        r.DataSourceCitations = x.score.DataSourceCitations;
                        r.UpdatedAt = x.score.UpdatedAt;
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();

                var finalResutl = new AiCityPillarReponseDto
                {
                    Pillars = result
                };

                var resposne = ResultResponseDto<AiCityPillarReponseDto>.Success(finalResutl, new[] { "Pillar get successfully", });

                return resposne;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return ResultResponseDto<AiCityPillarReponseDto>.Failure(new[] { "Error in getting pillar details", });
            }
        }
    }
}
