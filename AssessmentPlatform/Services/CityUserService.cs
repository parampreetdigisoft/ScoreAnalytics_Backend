﻿
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
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
        private bool IsPillarAccess(Enums.TieredAccessPlan tier, int order)
        {
            return tier switch
            {
                Enums.TieredAccessPlan.Basic => order <= 3,
                Enums.TieredAccessPlan.Standard => order <= 7,
                Enums.TieredAccessPlan.Premium => order <= 14,
                _ => false
            };
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

                // Then your aggregation logic
                cityHistory.TotalCity = cityQuery.Select(x => x.CityID).Distinct().Count();
                cityHistory.ActiveCity = cityQuery.Where(x => x.HasMapping).Select(x => x.CityID).Distinct().Count();

                var cityScores = cityQuery
                    .GroupBy(x => x.CityID)
                    .Select(g => Convert.ToDecimal(g.Sum(s => s.Score)) / (g.Count() * 4))
                    .ToList();

                if (cityScores.Any())
                {
                    cityHistory.AvgHighScore = cityScores.Max();
                    cityHistory.AvgLowerScore = cityScores.Min();
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
                // ✅ Pre-fetch total pillars and questions
                var pillarStats = await _context.Pillars
                    .Where(x => x.DisplayOrder < pillarPredicate)
                    .OrderBy(x => x.DisplayOrder)
                    .Select(p => new { p.PillarID, p.PillarName, QuestionCount = p.Questions.Count() })
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

                            return new CityPillarQuestionHistoryReponseDto
                            {
                                PillarID = p.PillarID,
                                PillarName = p.PillarName,
                                Score = totalAnsScore,
                                ScoreProgress = progress,
                                AnsPillar = paGroup.Any() ? 1 : 0,
                                TotalQuestion = p.QuestionCount * userCount,
                                AnsQuestion = paGroup.Sum(pg => pg.Responses.Count)
                            };
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
        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync1(PaginationRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
                if (user == null || user.Role != UserRole.CityUser)
                {
                    return new PaginationResponse<CityResponseDto>();
                }

                var year = DateTime.Now.Year;

                var cityQuery =
                   from c in _context.Cities
                   join uc in _context.UserCityMappings on c.CityID equals uc.CityID into ucg
                   from uc in ucg.DefaultIfEmpty()
                   join a in _context.Assessments on uc.UserCityMappingID equals a.UserCityMappingID into ag
                   from a in ag.DefaultIfEmpty()
                   join pa in _context.PillarAssessments on a.AssessmentID equals pa.AssessmentID into pag
                   from pa in pag.DefaultIfEmpty()
                   join r in _context.AssessmentResponses on pa.PillarAssessmentID equals r.PillarAssessmentID into rg
                   from r in rg.DefaultIfEmpty()
                   where !c.IsDeleted && (uc == null || !uc.IsDeleted) && (a == null || a.UpdatedAt.Year == year)
                   group r by new
                   {
                       c.CityID,
                       c.Country,
                       c.PostalCode,
                       c.Image,
                       c.State,
                       c.CityName,
                       c.Region,
                       c.IsActive,
                       c.CreatedDate,
                       c.UpdatedDate,
                       c.IsDeleted,
                       EvaluatorCount = _context.UserCityMappings
                                           .Count(x => x.CityID == c.CityID && !x.IsDeleted)  
                   }
                   into g
                   select new CityResponseDto
                   {
                       CityID = g.Key.CityID,
                       State = g.Key.State,
                       CityName = g.Key.CityName,
                       PostalCode = g.Key.PostalCode,
                       Region = g.Key.Region,
                       IsActive = g.Key.IsActive,
                       CreatedDate = g.Key.CreatedDate,
                       UpdatedDate = g.Key.UpdatedDate,
                       IsDeleted = g.Key.IsDeleted,
                       Country = g.Key.Country,
                       Image = g.Key.Image,
                       Score = g.Sum(x => (int?)x.Score ?? 0) * 100M / (g.Key.EvaluatorCount == 0 ? 1 : g.Key.EvaluatorCount) 
                   };
                var response = await cityQuery.ApplyPaginationAsync(
                    request,
                    x => string.IsNullOrEmpty(request.SearchText) ||
                         x.CityName.Contains(request.SearchText) ||
                         x.State.Contains(request.SearchText)
                );

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCitiesAsync", ex);
                return new PaginationResponse<CityResponseDto>();
            }
        }
        public async Task<PaginationResponse<CityResponseDto>> GetCitiesAsync(PaginationRequest request)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.UserID == request.UserId);

                if (user == null || user.Role != UserRole.CityUser)
                    return new PaginationResponse<CityResponseDto>();

                int currentYear = DateTime.Now.Year;

                // Base city query
                var query =
                    from c in _context.Cities
                    where !c.IsDeleted
                    select new
                    {
                        City = c,
                        // Average score for the current year
                        Score = (
                            from uc in _context.UserCityMappings
                            join a in _context.Assessments on uc.UserCityMappingID equals a.UserCityMappingID
                            join pa in _context.PillarAssessments on a.AssessmentID equals pa.AssessmentID
                            join r in _context.AssessmentResponses on pa.PillarAssessmentID equals r.PillarAssessmentID
                            where uc.CityID == c.CityID
                                  && !uc.IsDeleted
                                  && a.UpdatedAt.Year == currentYear
                            select (int?)r.Score
                        ).Average() ?? 0
                    };

                // Project to response DTO
                var cityDtos = query.Select(x => new CityResponseDto
                {
                    CityID = x.City.CityID,
                    CityName = x.City.CityName,
                    State = x.City.State,
                    Region = x.City.Region,
                    PostalCode = x.City.PostalCode,
                    Country = x.City.Country,
                    Image = x.City.Image,
                    CreatedDate = x.City.CreatedDate,
                    UpdatedDate = x.City.UpdatedDate,
                    IsActive = x.City.IsActive,
                    Score = Convert.ToDecimal(x.Score) 
                });

                // Apply search filter
                if (!string.IsNullOrEmpty(request.SearchText))
                {
                    string search = request.SearchText.ToLower();
                    cityDtos = cityDtos.Where(x =>
                        x.CityName.ToLower().Contains(search) ||
                        x.State.ToLower().Contains(search));
                }

                // Apply pagination
                var response = await cityDtos
                    .OrderByDescending(x => x.Score)
                    .ApplyPaginationAsync(request);

                return response;
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
                    .OrderBy(x=>x.DisplayOrder)
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

                        var totalQuestions = p.Questions.Count();
                        var answeredQuestions = responses.Count;
                        var totalScore = responses.Sum(r => (decimal?)r.Score ?? 0);

                        var scoreProgress = answeredQuestions > 0
                            ? (totalScore * 100M) / (answeredQuestions * 4M * assessments.Count)
                            : 0M;

                        var isAccess = IsPillarAccess(userCityRequstDto.Tiered, p.DisplayOrder);

                        if (isAccess)
                        {
                            return new CityPillarDetailsDto
                            {
                                PillarID = p.PillarID,
                                PillarName = p.PillarName,
                                TotalPillar = paForPillar.Count,
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
                                PillarName = p.PillarName
                            };
                        }
                    })
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
                        var totalQuestions = 1; // Each item represents one question
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

    }
}
