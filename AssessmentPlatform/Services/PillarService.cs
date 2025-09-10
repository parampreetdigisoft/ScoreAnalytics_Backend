using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class PillarService : IPillarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public PillarService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        public async Task<List<Pillar>> GetAllAsync()
        {
            try
            {
                return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<Pillar>();
            }

        }

        public async Task<Pillar> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Pillars.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> AddAsync(Pillar pillar)
        {
            try
            {
                _context.Pillars.Add(pillar);
                await _context.SaveChangesAsync();
                return pillar;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar)
        {
            try
            {
                var existing = await _context.Pillars.FindAsync(id);
                if (existing == null) return null;
                existing.PillarName = pillar.PillarName;
                existing.Description = pillar.Description;
                existing.DisplayOrder = pillar.DisplayOrder;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return new Pillar();
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var pillar = await _context.Pillars.FindAsync(id);
                if (pillar == null) return false;
                _context.Pillars.Remove(pillar);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return false;
            }

        }
        public async Task<ResultResponseDto<List<PillarsHistroyResponseDto>>> GetPillarsHistoryByUserId(GetCityPillarHistoryRequestDto request)
        {
            try
            {
                // 1. Validate user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserID == request.UserID);

                if (user == null)
                    return ResultResponseDto<List<PillarsHistroyResponseDto>>.Failure(new[] { "Invalid user" });

                // 2. Build UserCityMapping filter based on role
                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == request.CityID &&
                                             (x.AssignedByUserId == request.UserID || x.UserID == request.UserID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == request.CityID && x.UserID == request.UserID,
                    _ => x => !x.IsDeleted && x.CityID == request.CityID
                };

                // 3. Get all relevant UserCityMapping IDs
                var userCityMappingIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                // 4. Get all relevant pillar assessments
                var pillarAssessmentsQuery = _context.Assessments
                    .Where(a => userCityMappingIds.Contains(a.UserCityMappingID) && a.IsActive)
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => !request.PillarID.HasValue || pa.PillarID == request.PillarID);

                // 5. Get all pillars and join with assessments
                var pillarQuery = from p in _context.Pillars
                                  .Where(x => !request.PillarID.HasValue || x.PillarID == request.PillarID)
                                  join pa in pillarAssessmentsQuery on p.PillarID equals pa.PillarID into paGroup
                                  from pa in paGroup.DefaultIfEmpty()
                                  select new
                                  {
                                      p.PillarID,
                                      p.PillarName,
                                      p.DisplayOrder,
                                      UserID = pa != null ? pa.Assessment.UserCityMapping.UserID : 0,
                                      Responses = pa != null ? pa.Responses : null,
                                      TotalQuestion = p.Questions.Count()
                                  };

                var pillarList = (await pillarQuery.ToListAsync())
                    .Select(x =>
                    {
                        var responses = x.Responses ?? Enumerable.Empty<AssessmentResponse>();
                        var validResponses = responses
                            .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                            .ToList();

                        return new
                        {
                            x.PillarID,
                            x.PillarName,
                            x.DisplayOrder,
                            UserID = validResponses.Any() ? x.UserID : 0,
                            Score = validResponses.Sum(r => (int?)r.Score ?? 0),
                            ScoreCount = validResponses.Count,
                            x.TotalQuestion,
                            AnsQuestion = responses.Count(),
                            HasAnswer = responses.Any()
                        };
                    })
                    .ToList();

                if (!pillarList.Any())
                    return ResultResponseDto<List<PillarsHistroyResponseDto>>.Failure(new[] { "No pillar submitted" });

                // 6. Preload user dictionary to avoid N+1 queries
                var userIds = pillarList.Select(x => x.UserID).Where(id => id > 0).Distinct().ToList();
                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                // 7. Aggregate per pillar and per user
                var cityPillars = pillarList
                .GroupBy(x => new { x.PillarID, x.PillarName, x.DisplayOrder })
                .Select(g => new PillarsHistroyResponseDto
                {
                    PillarID = g.Key.PillarID,
                    PillarName = g.Key.PillarName,
                    DisplayOrder = g.Key.DisplayOrder,
                    Users = g.GroupBy(x => x.UserID)
                    .Where(ug => ug.Key > 0)
                    .Select(ug =>
                    {
                        var totalScore = ug.Sum(x => x.Score);
                        var scoreCount = ug.Sum(x => x.ScoreCount);
                        var ansUserCount = ug.Select(x => x.UserID).Distinct().Count();
                        var totalQuestionsInPillar = ug.Max(x => x.TotalQuestion) * ansUserCount;

                        decimal progress = scoreCount > 0 && ansUserCount > 0
                            ? totalScore * 100m / (scoreCount * 4m * ansUserCount)
                            : 0m;

                        return new PillarsUserHistroyResponseDto
                        {
                            FullName = usersDict.TryGetValue(ug.Key, out var name) ? name : "",
                            UserID = ug.Key,
                            Score = totalScore,
                            ScoreProgress = progress,
                            AnsPillar = g.Count(x => x.HasAnswer),
                            TotalQuestion = totalQuestionsInPillar,
                            AnsQuestion = g.Sum(x => x.AnsQuestion),
                        };
                    }).ToList()
                }).OrderBy(x => x.DisplayOrder).ToList();

                return ResultResponseDto<List<PillarsHistroyResponseDto>>.Success(cityPillars);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetPillarsHistoryByUserId", ex);
                return ResultResponseDto<List<PillarsHistroyResponseDto>>.Failure(new[] { "There is an error, please try later" });
            }
        }
    }
}