
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
                    return ResultResponseDto<CityHistoryDto>.Failure(new string[] { "Invalid request" });
                }
                var date = DateTime.Now;
                var cityHistory = new CityHistoryDto();

                Expression<Func<UserCityMapping, bool>> predicate = x => !x.IsDeleted;



                // 1️⃣ Get city-related counts in a single round trip
                var cityQuery = await (
                    from c in _context.Cities
                    where !c.IsDeleted && c.IsActive
                    join uc in _context.UserCityMappings.Where(predicate)
                        on c.CityID equals uc.CityID into cityMappings
                    from uc in cityMappings.DefaultIfEmpty()
                    join a in _context.Assessments.Where(x => x.IsActive && x.UpdatedAt.Year == date.Year)
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

                cityHistory.TotalEvaluator = 1;
                cityHistory.TotalAnalyst = 1;

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
        public async Task<ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>> GetCitiesProgressByUserId(int userID)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.CityUser);
                if (user == null)
                {
                    return ResultResponseDto<List<GetCitiesSubmitionHistoryReponseDto>>.Failure(new string[] { "Invalid request" });
                }

                var date = DateTime.Now;

                // Get total pillars and questions (independent query)
                var pillarStats = await _context.Pillars
                    .Select(p => new { QuestionsCount = p.Questions.Count() })
                    .ToListAsync();

                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                Expression<Func<UserCityMapping, bool>> predicate = x => !x.IsDeleted;

                var cityRaw = await (
                    from uc in _context.UserCityMappings.Where(predicate)
                    join c in _context.Cities.Where(c => !c.IsDeleted && c.IsActive)
                        on uc.CityID equals c.CityID
                    join a in _context.Assessments.Where(x => x.IsActive && x.UpdatedAt.Year == date.Year)
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
                        var allResponses = g.Where(x => x.Responses != null).SelectMany(p => p.Responses);
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
        public async Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto)
        {
            try
            {
                var userID = userCityRequstDto.UserID;
                var cityID = userCityRequstDto.CityID;
                DateTime date = DateTime.Now;

                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID && x.Role == UserRole.CityUser);
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
                var cityHistory = new CityHistoryDto();

                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {

                    UserRole.CityUser => x => !x.IsDeleted && x.CityID == cityID,
                    _ => x => !x.IsDeleted && x.CityID == cityID
                };


                // 1. Get all UserCityMapping IDs for the city
                var ucmIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                var pillarAssessments = _context.Assessments
                    .Where(a => ucmIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == date.Year)
                    .SelectMany(x => x.PillarAssessments);

                // 2. Fetch city-wise pillar/question details in one go
                var cityPillarQuery =
                    from p in _context.Pillars
                    join pa in pillarAssessments on p.PillarID equals pa.PillarID into paGroup
                    from pa in paGroup.DefaultIfEmpty()
                    select new
                    {
                        p.PillarID,
                        p.PillarName,
                        UserID = pa != null && pa.Responses
                                .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                                .Count() > 0 ? pa.Assessment.UserCityMapping.UserID : 0,
                        Score = pa != null
                            ? pa.Responses
                                .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                                .Sum(r => (int?)r.Score ?? 0)
                            : 0,
                        ScoreCount = pa != null ? pa.Responses.Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four).Count() : 0,
                        TotalQuestion = p.Questions.Count(),
                        AnsQuestion = pa != null ? pa.Responses.Count() : 0,
                        HasAnswer = pa != null
                    };

                var cityPillars = (await cityPillarQuery.ToListAsync())
                    .GroupBy(x => new { x.PillarID, x.PillarName })
                    .Select(g =>
                    {
                        var totalAnsScoreOfPillar = g.Sum(x => x.Score);
                        var ScoreCount = g.Sum(x => x.ScoreCount);
                        var ansUserCount = g.Where(x => x.UserID > 0).Distinct().Count();
                        var totalQuestionsInPillar = g.Max(x => x.TotalQuestion) * ansUserCount;

                        decimal progress = ScoreCount != 0 && ansUserCount > 0 ? totalAnsScoreOfPillar * 100 / (ScoreCount * 4m * ansUserCount) : 0m;

                        return new CityPillarQuestionHistoryReponseDto
                        {
                            PillarID = g.Key.PillarID,
                            PillarName = g.Key.PillarName,
                            Score = totalAnsScoreOfPillar,
                            ScoreProgress = progress,
                            AnsPillar = g.Sum(x => x.HasAnswer ? 1 : 0),
                            TotalQuestion = totalQuestionsInPillar,
                            AnsQuestion = g.Sum(x => x.AnsQuestion)
                        };
                    })
                    .ToList();

                //// 3. Get assessment count in one query
                //var assessmentCount = await _context.Assessments
                //    .CountAsync(x => ucmIds.Contains(x.UserCityMappingID) && x.IsActive);

                //// 4. Total pillars and questions (static across city)
                //var pillarStats = await _context.Pillars
                //    .Select(p => new { QuestionsCount = p.Questions.Count() })
                //    .ToListAsync();
                //int totalPillars = pillarStats.Count;
                //int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                // 5. Final payload
                var payload = new GetCityQuestionHistoryReponseDto
                {
                    CityID = cityID,
                    //TotalAssessment = assessmentCount,
                    //Score = cityPillars.Sum(x => x.Score),
                    //ScoreProgress = cityPillars.Sum(x => x.ScoreProgress)/ totalPillars,
                    //TotalPillar = totalPillars * ucmIds.Count,
                    //TotalAnsPillar = cityPillars.Sum(x => x.AnsPillar),
                    //TotalQuestion = totalQuestions * ucmIds.Count,
                    //AnsQuestion = cityPillars.Sum(x => x.AnsQuestion),
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
    }
}
