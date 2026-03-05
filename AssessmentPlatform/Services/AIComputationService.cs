using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace AssessmentPlatform.Services
{
    public class AIComputationService : IAIComputationService
    {
        #region constructor
        
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;
        private readonly Download _download;
        private readonly IAIAnalyzeService _iAIAnalayzeService;
        public AIComputationService(ApplicationDbContext context, IAppLogger appLogger, ICommonService commonService, Download download, IAIAnalyzeService iAIAnalayzeService)
        {
            _context = context;
            _appLogger = appLogger;
            _commonService = commonService;
            _download = download;
            _iAIAnalayzeService = iAIAnalayzeService;
        }
        #endregion

        #region implementation

     
        public async Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels()
        {
            var r = await _context.AITrustLevels.ToListAsync();

            return ResultResponseDto<List<AITrustLevel>>.Success(r, new[] { "Pillar get successfully" });

        }
        public async Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                IQueryable<AiCitySummeryDto> query = await GetCityAiSummeryDetails(userID, userRole, request.CityID, request.Year);

                var result = await query.ApplyPaginationAsync(request); 

                if(userRole != UserRole.CityUser)
                {
                    var progress = await _commonService.GetCitiesProgressAsync(userID, (int)userRole, DateTime.Now.Year);

                    var ids = result.Data.Select(x => x.CityID);
                    var cities = progress.Where(x => ids.Contains(x.CityID));


                    var counts = await _context.Pillars
                        .Select(p => p.Questions.Count()).ToListAsync();

                    var totalQuestions = counts.Sum();

                    var answeredQuestions = await _context.AIEstimatedQuestionScores
                        .Where(x => x.Year == request.Year && ids.Contains(x.CityID))
                        .GroupBy(x => x.CityID)
                        .Select(g => new
                        {
                            CityID = g.Key,
                            CompletionRate = totalQuestions == 0
                                ? 0
                                : g.Count() * 100.0M / totalQuestions
                        })
                        .ToListAsync();


                    foreach (var c in result.Data)
                    {
                        var pillars = cities.Where(x => x.CityID == c.CityID);

                        var cityScore = pillars.Select(x => x.ScoreProgress)
                            .DefaultIfEmpty(0)
                            .Average();

                        c.EvaluatorProgress = cityScore;
                        c.Discrepancy = Math.Abs(cityScore - (c.AIProgress ?? 0));
                        c.AICompletionRate = answeredQuestions.FirstOrDefault(x=>x.CityID== c.CityID)?.CompletionRate;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetCitiesAsync", ex);
                return new PaginationResponse<AiCitySummeryDto>();
            }
        }
        public async Task<IQueryable<AiCitySummeryDto>> GetCityAiSummeryDetails(int userID, UserRole userRole, int? cityID, int currentYear=0)
        {
            currentYear = currentYear ==0 ? DateTime.Now.Year : currentYear;
            var firstDate = new DateTime(currentYear, 1, 1); 
            var endDate = new DateTime(currentYear+1, 1, 1); 
            IQueryable<AICityScore> baseQuery = _context.AICityScores.Where(x=> x.UpdatedAt >= firstDate && x.UpdatedAt < endDate && x.Year== currentYear);

            List<int> allowedCityIds = new();
            if (userRole == UserRole.Analyst)
            {
                // Allowed city IDs
                 allowedCityIds = await _context.UserCityMappings
                            .Where(x => !x.IsDeleted && x.UserID == userID && (!cityID.HasValue || x.CityID == cityID.Value))
                            .Select(x => x.CityID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
            }
            else if (userRole == UserRole.Evaluator)
            {
                // Allowed city IDs
                 allowedCityIds = await _context.AIUserCityMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!cityID.HasValue || x.CityID == cityID.Value))
                            .Select(x => x.CityID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
            }
            else if (userRole == UserRole.CityUser)
            {
                 allowedCityIds = await _context.PublicUserCityMappings
                            .Where(x => x.IsActive && x.UserID == userID && (!cityID.HasValue || x.CityID == cityID.Value))
                            .Select(x => x.CityID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID) && x.IsVerified);
            }
            else
            {
                // Admin
                if (cityID.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.CityID == cityID.Value);
                    allowedCityIds = new() { cityID.Value };
                }
            }
            var commentQuery = _context.AIUserCityMappings
                .Where(x =>
                    (
                        userRole == UserRole.Admin ||
                        (userRole == UserRole.Analyst && x.AssignBy == userID) ||
                        (userRole == UserRole.Evaluator && x.UserID == userID)
                    )
                )
                .GroupBy(x => x.CityID)
                .Select(g => new
                {
                    CityID = g.Key,
                    Comment = g
                        .OrderByDescending(x => x.UpdatedAt >= firstDate && x.UpdatedAt < endDate)
                        .Select(x => x.Comment)
                        .FirstOrDefault()
                });

            var query =
                from c in _context.Cities
                where allowedCityIds.Contains(c.CityID) || (userRole == UserRole.Admin && !cityID.HasValue)

                join score in baseQuery
                    on c.CityID equals score.CityID
                    into scoreJoin
                from score in scoreJoin.DefaultIfEmpty()   // LEFT JOIN score

                join cmt in commentQuery
                    on c.CityID equals cmt.CityID
                    into cmtJoin
                from cmt in cmtJoin.DefaultIfEmpty()       // LEFT JOIN comment

                select new AiCitySummeryDto
                {
                    CityID = c.CityID,
                    State = c.State ?? string.Empty,
                    CityName = c.CityName ?? string.Empty,
                    Country = c.Country ?? string.Empty,
                    Image = c.Image ?? string.Empty,

                    ScoringYear = score != null ? score.Year : currentYear,
                    AIScore = score != null ? score.AIScore : 0,
                    AIProgress = score != null ? score.AIProgress : null,
                    EvaluatorProgress = score != null ? score.EvaluatorProgress : null,
                    Discrepancy = score != null ? score.Discrepancy : null,
                    ConfidenceLevel = score != null ? score.ConfidenceLevel : string.Empty,

                    EvidenceSummary = score != null ? score.EvidenceSummary : string.Empty,
                    CrossPillarPatterns = score != null ? score.CrossPillarPatterns : string.Empty,
                    InstitutionalCapacity = score != null ? score.InstitutionalCapacity : string.Empty,
                    EquityAssessment = score != null ? score.EquityAssessment : string.Empty,
                    SustainabilityOutlook = score != null ? score.SustainabilityOutlook : string.Empty,
                    StrategicRecommendations = score != null ? score.StrategicRecommendations : string.Empty,
                    DataTransparencyNote = score != null ? score.DataTransparencyNote : string.Empty,

                    UpdatedAt = score != null ? score.UpdatedAt : null,
                    IsVerified = score != null ? score.IsVerified : false,

                    Comment = cmt != null ? cmt.Comment : null
                };



            return query;
        }
    
        public async Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(int cityID, int userID, UserRole userRole, int currentYear = 0)
        {
            try
            {
                currentYear = currentYear == 0 ? DateTime.Now.Year : currentYear;
                var firstDate = new DateTime(currentYear, 1, 1);

                var res = await _context.AIPillarScores
                    .Where(x => x.CityID == cityID && x.UpdatedAt >= firstDate && x.Year == currentYear)
                    .Include(x=>x.City)
                    .Include(x => x.DataSourceCitations)
                    .ToListAsync();

                List<int> pillarIds = new();
                if (userRole == UserRole.CityUser)
                {
                    pillarIds = await _context.CityUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID)
                                .Distinct()
                                .ToListAsync();
                }
                var pillars = await _context.Pillars.Select(x=>new
                {
                    PillarID = x.PillarID,
                    PillarName = x.PillarName,
                    DisplayOrder = x.DisplayOrder,
                    ImagePath = x.ImagePath,
                    TotalQuestions = x.Questions.Count()
                }).ToListAsync();

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
                        CityID = x.score?.CityID ?? cityID,
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
                        r.EvaluatorProgress = x.score.EvaluatorProgress;
                        r.Discrepancy = x.score.Discrepancy;
                        r.ConfidenceLevel = x.score.ConfidenceLevel;
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


                var progress = await _commonService.GetCitiesProgressAsync(userID, (int)userRole, currentYear);

                var cities = progress.Where(x => x.CityID== cityID);

                var answeredQuestions = await _context.AIEstimatedQuestionScores
               .Where(x => x.Year == currentYear && x.CityID == cityID)
               .GroupBy(x => x.PillarID)
               .Select(g => new
               {
                   PillarID = g.Key,
                   AnsweredQuestions = g.Count() 
               })
               .ToListAsync();

                foreach (var c in result)
                {
                    var totalQuestions = pillars.FirstOrDefault(x => x.PillarID == c.PillarID)?.TotalQuestions ?? 1;
                    var answeredQuestion = answeredQuestions.FirstOrDefault(x => x.PillarID == c.PillarID)?.AnsweredQuestions ?? 0;

                    var cityScore = cities
                        .Where(x => x.PillarID == c.PillarID)
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Average();


                    c.EvaluatorProgress = cityScore;
                    c.Discrepancy = Math.Abs(cityScore - (c.AIProgress ?? 0));
                    c.AICompletionRate = answeredQuestion * 100.0M / totalQuestions;
                }

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
        public async Task<PaginationResponse<AIEstimatedQuestionScoreDto>> GetAIPillarsQuestion(AiCityPillarSummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                if (userRole == UserRole.CityUser && request.CityID != null && request.PillarID != null)
                {
                    var isPillarAccess = _context.CityUserPillarMappings
                                .Where(x => x.IsActive && x.UserID == userID)
                                .Select(x => x.PillarID).Contains(request.PillarID.Value);

                    var isCityAccess = _context.PublicUserCityMappings
                               .Where(x => x.IsActive && x.UserID == userID)
                               .Select(x => x.CityID).Contains(request.CityID.Value);
                    if (!(isCityAccess && isPillarAccess))
                    {
                        return new PaginationResponse<AIEstimatedQuestionScoreDto>();
                    }
                }
                var currentYear = request.Year;
                var firstDate = new DateTime(currentYear, 1, 1);

                var res =
                    from q in _context.Questions.Where(x=>x.PillarID== request.PillarID)
                    join s in _context.AIEstimatedQuestionScores
                        .Where(x =>
                            x.CityID == request.CityID &&
                            x.PillarID == request.PillarID &&
                            x.UpdatedAt >= firstDate && x.Year == currentYear)
                    on q.QuestionID equals s.QuestionID into qs
                    from x in qs.DefaultIfEmpty() // LEFT JOIN
                    select new AIEstimatedQuestionScoreDto
                    {
                        CityID = x == null ? request.CityID ??0  : x.CityID,
                        PillarID = x == null ? request.PillarID ?? 0 : x.PillarID,
                        QuestionID = q.QuestionID,
                        DataYear = x == null ? currentYear : x.Year,
                        AIScore = x == null ? null : x.AIScore,
                        AIProgress = x == null ? null : x.AIProgress,
                        EvaluatorProgress = x == null ? null : x.EvaluatorProgress,
                        Discrepancy = x == null ? null : x.Discrepancy,
                        ConfidenceLevel = x == null ? string.Empty : x.ConfidenceLevel,
                        DataSourcesUsed = x == null ? null : x.DataSourcesUsed,
                        EvidenceSummary = x == null ? string.Empty : x.EvidenceSummary,
                        RedFlags = x == null ? string.Empty : x.RedFlags,
                        GeographicEquityNote = x == null ? string.Empty : x.GeographicEquityNote,
                        SourceType = x == null ? string.Empty : x.SourceType,
                        SourceName = x == null ? string.Empty : x.SourceName,
                        SourceURL = x == null ? string.Empty : x.SourceURL,
                        SourceDataExtract = x == null ? string.Empty : x.SourceDataExtract,
                        SourceDataYear = x == null ? null : x.SourceDataYear,
                        SourceTrustLevel = x == null ? null : x.SourceTrustLevel,
                        UpdatedAt = x == null ? null : x.UpdatedAt,
                        QuestionText = q.QuestionText == null ? string.Empty : q.QuestionText
                    };

                var r = await res.ApplyPaginationAsync(request);

                return r;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return new PaginationResponse<AIEstimatedQuestionScoreDto>();
            }
        }
        public async Task<ResultResponseDto<AiCrossCityResponseDto>> GetAICrossCityPillars(AiCityIdsDto cityIds, int userID, UserRole userRole)
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                var response = new AiCrossCityResponseDto();

                var firstDate = new DateTime(currentYear, 1, 1);

                var aiPillarScores = await _context.AIPillarScores
                    .Where(x => cityIds.CityIDs.Contains(x.CityID) && x.UpdatedAt >= firstDate)
                    .ToListAsync();

                var cities = await _context.Cities
                    .Where(x => cityIds.CityIDs.Contains(x.CityID))
                    .ToListAsync();

                // Pillar access based on role
                List<int> pillarIds = new();
                if (userRole == UserRole.CityUser)
                {
                    pillarIds = await _context.CityUserPillarMappings
                        .Where(x => x.IsActive && x.UserID == userID)
                        .Select(x => x.PillarID)
                        .Distinct()
                        .ToListAsync();
                }

                var pillars = await _context.Pillars.ToListAsync();

                // Categories
                response.Categories.AddRange(
                    pillars
                        .Where(x => pillarIds.Count == 0 || pillarIds.Contains(x.PillarID))
                        .OrderBy(x=>x.DisplayOrder)
                        .Select(x => x.PillarName)
                );
                // Per city processing

                var aiCities = await _context.AICityScores
                    .Where(x => cityIds.CityIDs.Contains(x.CityID) &&
                                x.Year == currentYear && ((userRole == UserRole.CityUser && x.IsVerified) || userRole != UserRole.CityUser))
                    .GroupBy(x => x.CityID)
                    .Select(g => new
                    {
                        CityID = g.Key,
                        AIProgress = g.Max(x => x.AIProgress)
                    })
                    .ToDictionaryAsync(x => x.CityID, x => x.AIProgress);


                foreach (var city in cities)
                {
                    var pillarResults = pillars
                    .GroupJoin(
                        aiPillarScores.Where(x => x.CityID == city.CityID),
                        p => p.PillarID,
                        s => s.PillarID,
                        (pillar, scores) => new
                        {
                            Pillar = pillar,
                            Score = scores.FirstOrDefault()
                        })
                    .Select(x =>
                    {
                        var isAccess = pillarIds.Count == 0 || pillarIds.Contains(x.Pillar.PillarID);

                        return new CrossCityPillarValueDto
                        {
                            PillarID = x.Pillar.PillarID,
                            PillarName = x.Pillar.PillarName,
                            Value = isAccess ? x.Score?.AIProgress ?? 0 : 0,
                            IsAccess = isAccess,
                            DisplayOrder = x.Pillar.DisplayOrder
                        };
                    })
                    .OrderBy(x => !x.IsAccess)
                    .ThenBy(x => x.DisplayOrder)
                    .ToList();
                    var chartRow = new CrossCityChartTableRowDto
                    {
                        CityID = city.CityID,
                        CityName = city.CityName,
                        PillarValues = pillarResults.ToList()
                    };
                    if (aiCities?.TryGetValue(city.CityID,out var aiCityValue) ?? false)
                    {
                        chartRow.Value = aiCityValue ?? 0;
                    }
                    response.TableData.Add(chartRow);

                    var series = new CrossCityChartSeriesDto
                    {
                        Name = city.CityName,
                        Data = pillarResults
                            .Where(x => x.IsAccess)
                            .Select(x => x.Value).ToList()
                    };
                    response.Series.Add(series);
                }

                return ResultResponseDto<AiCrossCityResponseDto>.Success(response,new[] { "Pillars fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAICrossCityPillars", ex);
                return ResultResponseDto<AiCrossCityResponseDto>.Failure(new[] { "Error in getting pillar details" });
            }
        }

        public async Task<ResultResponseDto<bool>> ChangedAiCityEvaluationStatus(ChangedAiCityEvaluationStatusDto dto, int userID, UserRole userRole)
        {
            try
            {
                var v = _context.UserCityMappings.Any(x => x.UserID == userID && x.CityID == dto.CityID);
                if ((v && userRole == UserRole.Analyst) || userRole == UserRole.Admin)
                {

                    var aiResponse = await _context.AICityScores.Where(x => x.CityID == dto.CityID && x.Year == DateTime.UtcNow.Year).FirstOrDefaultAsync();
                    if (aiResponse != null)
                    {
                        aiResponse.IsVerified = dto.IsVerified;
                        aiResponse.VerifiedBy = userID;
                        
                        await _context.SaveChangesAsync();

                        _download.InsertAnalyticalLayerResults(dto.CityID);
                        return ResultResponseDto<bool>.Success(true, new[] { dto.IsVerified ? "Finalize and lock the AI-generated score successfully" : "Reject the current AI-generated score Successfully" });
                    }
                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid city, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiCityEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiCity Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegenerateAiSearch(RegenerateAiSearchDto dto,int userID, UserRole userRole)
        {
            try
            {
                if (dto.QuestionEnable)
                {
                    var currentYear = DateTime.Now.Year;

                    var aiQuestionList = await _context.AIEstimatedQuestionScores
                        .Where(x => x.CityID == dto.CityID && x.Year == currentYear)
                        .ToListAsync();

                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }
                }


                await _download.AiResearchByCityId(dto.CityID, dto.CityEnable, dto.PillarEnable, dto.QuestionEnable);
                var aiResponse = await _context.AICityScores.FirstOrDefaultAsync(x => x.CityID == dto.CityID);
                if(aiResponse != null)
                {
                    aiResponse.IsVerified = false;
                }
                // Assign viewers (optional)

                var aIUserCityMappingsList = await _context.AIUserCityMappings.Where(x => x.CityID == dto.CityID).ToListAsync();

                var um = _context.UserCityMappings.Where(x => !x.IsDeleted && x.CityID == dto.CityID && dto.ViewerUserIDs.Contains(x.UserID));
                var valid = um.All(x => dto.ViewerUserIDs.Contains(x.UserID));

                string msg = "Evaluator not have access of this city please try again";

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any() && valid)
                {
                    var existingMappings = aIUserCityMappingsList.Where(x => dto.ViewerUserIDs.Contains(x.UserID));


                    var existingUserIds = existingMappings.Select(x => x.UserID).ToHashSet();

                    // Update existing mappings
                    foreach (var mapping in existingMappings)
                    {
                        mapping.IsActive = true;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }

                    // Insert new mappings
                    var newMappings = dto.ViewerUserIDs
                        .Where(userId => !existingUserIds.Contains(userId))
                        .Select(userId => new AIUserCityMapping
                        {
                            UserID = userId,
                            CityID = dto.CityID,
                            AssignBy = userID,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });

                    await _context.AIUserCityMappings.AddRangeAsync(newMappings);
                    msg = "Evaluator have access to view the city";
                }
                else if(aIUserCityMappingsList.Count > 0)
                {
                    foreach (var mapping in aIUserCityMappingsList)
                    {
                        mapping.IsActive = false;
                        mapping.UpdatedAt = DateTime.UtcNow;
                        mapping.AssignBy = userID;
                        mapping.Comment = string.Empty;
                    }
                }

                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };

                if (dto.ViewerUserIDs != null && dto.ViewerUserIDs.Any())
                {
                    msglist.Add(msg);
                }
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }
        public async Task<ResultResponseDto<bool>> AddComment(AddCommentDto dto, int userID, UserRole userRole)
        {
            try
            {
                var aIUserCityMappings = await _context.AIUserCityMappings.FirstOrDefaultAsync(x => x.UserID == userID && x.IsActive && x.CityID == dto.CityID);
                if (aIUserCityMappings !=null && userRole == UserRole.Evaluator)
                {
                    aIUserCityMappings.Comment = dto.Comment;

                    await _context.SaveChangesAsync();


                    await _context.SaveChangesAsync();
                    return ResultResponseDto<bool>.Success(true, new[] {"Comment Added Successfully"});

                }
                return ResultResponseDto<bool>.Failure(new[] { "Invalid city, please try again" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangedAiCityEvaluationStatus", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Error in Changed AiCity Evaluation Status" });
            }
        }
        public async Task<ResultResponseDto<bool>> RegeneratePillarAiSearch(RegeneratePillarAiSearchDto channel, int userID, UserRole userRole)
        {
            try
            {
                if (channel.QuestionEnable)
                {
                    var currentYear = DateTime.Now.Year;
                    var aiQuestionList = await _context.AIEstimatedQuestionScores.Where(x => x.CityID == channel.CityID && x.PillarID== channel.PillarID && x.Year == currentYear).ToListAsync();
                    if (aiQuestionList.Count > 0)
                    {
                        _context.AIEstimatedQuestionScores.RemoveRange(aiQuestionList);
                        await _context.SaveChangesAsync();
                    }

                    await _iAIAnalayzeService.AnalyzeQuestionsOfCityPillar(channel.CityID, channel.PillarID);
                }

                if (channel.PillarEnable)
                    await _iAIAnalayzeService.AnalyzeSinglePillar(channel.CityID,channel.PillarID);


                var msglist = new List<string>
                {
                    "AI research import has been initiated successfully"
                };
               
                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, msglist);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RegenerateAiSearch", ex);

                return ResultResponseDto<bool>.Failure(new[] { "Something went wrong while importing AI research. Please try again later." });
            }
        }
        public async Task<AiCitySummeryDto> GetCityAiSummeryDetail(int userID, UserRole userRole, int? cityID, int year)
        {
            var query = await GetCityAiSummeryDetails(userID, userRole, cityID, year);
            var cityDetails = await query.FirstAsync();

            if (userRole != UserRole.CityUser)
            {
                var progress = await _commonService.GetCitiesProgressAsync(userID, (int)userRole, DateTime.Now.Year);

                var cities = progress.Where(x => x.CityID == cityID);

               if(cities != null)
                {
                    var cityScore = cities
                        .Select(x => x.ScoreProgress)
                        .DefaultIfEmpty(0)
                        .Average();

                    cityDetails.EvaluatorProgress = Math.Round(cityScore,2);
                    cityDetails.Discrepancy = Math.Abs(cityScore - (cityDetails.AIProgress ?? 0));
               }
            }
            return cityDetails;
        }

        #endregion

        
        #region pdf pillars and city report
            
        private async Task<List<KpiChartItem>> GetAccessKpis(
            int cityID, int userID, UserRole role, int year = 0)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year + 1, 1, 1);

            var baseQuery = _context.AnalyticalLayerResults
                .AsNoTracking()
                .Include(ar => ar.AnalyticalLayer)
                    .ThenInclude(al => al.FiveLevelInterpretations)
                .Include(ar => ar.City)
                .Where(x => x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate);

            if (role == UserRole.CityUser)
            {
                var validCities = _context.PublicUserCityMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.CityID);

                var validPillarIds = _context.CityUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userID)
                    .Select(x => x.PillarID);

                var validLayerIds = _context.AnalyticalLayerPillarMappings
                    .Where(x => validPillarIds.Contains(x.PillarID))
                    .Select(x => x.LayerID)
                    .Distinct();

                baseQuery = baseQuery
                    .Where(ar =>
                        validCities.Contains(ar.CityID) &&
                        validLayerIds.Contains(ar.LayerID));
            }

            var kpiRaw = baseQuery
                .Where(x => x.CityID == cityID)
                .Select(x => new
                {
                    KpiShortName = x.AnalyticalLayer.LayerCode,
                    KpiName = x.AnalyticalLayer.LayerName,
                    Value = x.AiCalValue5
                });

            var kpis = await kpiRaw
                .Select(k => new KpiChartItem(k.KpiShortName, k.KpiName, k.Value))
                .ToListAsync();

            return kpis ?? new List<KpiChartItem>();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  ENTRY POINTS  (GenerateCityDetailsPdf / GeneratePillarDetailsPdf)
        // ─────────────────────────────────────────────────────────────────────────────

        public async Task<byte[]> GenerateCityDetailsPdf(
            AiCitySummeryDto cityDetails, UserRole userRole, int userID)
        {
            try
            {
                var pillars = await GetAICityPillars(
                    cityDetails.CityID, userID, userRole, cityDetails.ScoringYear);

                var kpis = await GetAccessKpis(
                    cityDetails.CityID, userID, userRole, cityDetails.ScoringYear);

                // Build pillar chart items (max 14)
                var pillarChartItems = pillars.Result.Pillars
                    .Take(14)
                    .Select(p => new KpiChartItem(
                        p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName ?? "—",
                        p.PillarName ?? "—",
                        p.AIProgress))
                    .ToList();

                // KPI chart items capped at 107
                var kpiChartItems = kpis.Take(107).ToList();

                var document = Document.Create(container =>
                {
                    // ── Section 1 : Global Dashboard ─────────────────────────────────
                    AddGlobalDashboardPage(
                        container, cityDetails, pillarChartItems, kpiChartItems, userRole);


                    // ── Section 2 : City Summary ─────────────────────────────────────
                    container.Page(page =>
                    {
                        ApplyPageDefaults(page);
                        page.Header().Element(x =>
                            CityComposeHeader(x, cityDetails, userRole, null));
                        page.Content().Element(content =>
                        {
                            content.Column(column =>
                            {
                                column.Spacing(10);
                                column.Item().Element(x =>
                                    CitySummeryComposeContent(x, cityDetails, userRole));
                            });
                        });
                        PageFooter(page);
                    });

                    // ── Section 3 : Pillar Radial Overview ───────────────────────────
                    if (pillarChartItems.Any())
                    {
                        container.Page(page =>
                        {
                            ApplyPageDefaults(page);
                            page.Header().Element(x =>
                                CityComposeHeader(x, cityDetails, userRole, "Pillar Performance Overview"));
                            page.Content().Element(content =>
                                PillarLineChartPage(content, pillarChartItems));
                            PageFooter(page);
                        });
                    }

                    // ── Section 4+ : Per-Pillar Detail ──────────────────────────────
                    var accessiblePillars = pillars.Result.Pillars.Where(p => p.IsAccess).ToList();
                    foreach (var p in accessiblePillars)
                    {
                        container.Page(page =>
                        {
                            ApplyPageDefaults(page);
                            page.Header().Element(x =>
                                CityComposeHeader(x, cityDetails, userRole, p.PillarName));
                            page.Content().Element(content =>
                            {
                                content.Column(column =>
                                {
                                    column.Spacing(10);
                                    column.Item().Element(x =>
                                        PillarComposeContent(x, p, userRole));
                                });
                            });
                            PageFooter(page);
                        });
                    }

                    // ── Section 5 : KPI Dashboard ────────────────────────────────────
                    if (kpiChartItems.Any())
                    {
                        container.Page(page =>
                        {
                            ApplyPageDefaults(page);
                            page.Header().Element(x =>
                                CityComposeHeader(x, cityDetails, userRole, "KPI Dashboard"));
                            page.Content().Element(content =>
                                KpiDashboardPage(content, kpiChartItems));
                            PageFooter(page);
                        });
                    }

                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCityDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsPdf(
            AiCityPillarReponse pillarData, UserRole userRole)
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(25);
                        page.PageColor("#FAFAFA");
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));
                        page.Header().Element(header => PillarComposeHeader(header, pillarData));
                        page.Content().Element(content =>
                            PillarComposeContent(content, pillarData, userRole));
                        page.Footer().Element(PillarComposeFooter);
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  PAGE LAYOUT HELPERS  (reusable)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Applies standard A4 + font defaults to any page.</summary>
        static void ApplyPageDefaults(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(25);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
        }

        /// <summary>Standard numeric footer for city pages.</summary>
        static void PageFooter(PageDescriptor page)
        {
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
            });
        }

        
        /// <summary>
        /// Inserts the attractive full-page dashboard as page 1 of the city report.
        /// Pillars: max 14 · KPIs: max 107
        /// </summary>
        void AddGlobalDashboardPage(
            IDocumentContainer doc,
            AiCitySummeryDto city,
            List<KpiChartItem> pillars,   // already filtered to max 14
            List<KpiChartItem> kpis,      // already filtered to max 107
            UserRole userRole)
        {
            var vPillars = pillars.Where(p => p.Value.HasValue).ToList();
            var vKpis = kpis.Where(k => k.Value.HasValue).ToList();

            doc.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, city, userRole, "City Performance Dashboard"));
                page.Content().Element(x =>
                    RenderDashboardContent(x, vPillars, vKpis, city));
                PageFooter(page);
            });
        }

        void RenderDashboardContent(
            IContainer container,
            List<KpiChartItem> pillars,
            List<KpiChartItem> kpis,
            AiCitySummeryDto city)
        {
            float overall = (float)city.AIProgress.GetValueOrDefault();
            int kpiGreen = kpis.Count(k => k.Value >= 70);
            int kpiAmber = kpis.Count(k => k.Value >= 40 && k.Value < 70);
            int kpiRed = kpis.Count(k => k.Value < 40);
            var best = pillars.OrderByDescending(p => p.Value).FirstOrDefault();
            var worst = pillars.OrderBy(p => p.Value).FirstOrDefault();

            container.PaddingTop(6).Column(col =>
            {
                col.Spacing(10);

                // ── Row 1 : Score Donut (left)  +  Pillar Radar (right) ──────────
                col.Item().Height(280).Row(row =>
                {
                    row.RelativeItem(4).Element(x =>
                        RenderScoreDonutCard(x, overall, pillars.Count, kpis.Count, best, worst));

                    row.ConstantItem(10);

                    row.RelativeItem(6).Element(x =>
                        RenderPillarRadarCard(x, pillars));
                });

                // ── Row 2 : KPI distribution stat cards ──────────────────────────
                col.Item().Height(110).Element(x =>
                    RenderKpiDistributionBand(x, kpis.Count, kpiGreen, kpiAmber, kpiRed));

                // ── Row 3 : KPI sorted sparkline ─────────────────────────────────
                if (kpis.Any())
                    col.Item().Height(150).Element(x =>
                        RenderKpiSparklineCard(x, kpis));
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — Score Donut Card
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderScoreDonutCard(
            IContainer container,
            float score,
            int pillarCount,
            int kpiCount,
            KpiChartItem? best,
            KpiChartItem? worst)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(0);

                    col.Item().AlignCenter()
                        .Text("Overall City Score")
                        .FontSize(10).Bold().FontColor("#12352f");

                    // Donut chart
                    col.Item().Height(150).Canvas((canvas, size) =>
                        PaintDonut(canvas, size, score));

                    col.Item().Height(1).Background("#E8F0EC");

                    // Pillar count + KPI count
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text(pillarCount.ToString())
                                .FontSize(18).Bold().FontColor("#336b58");
                            c.Item().AlignCenter().Text("Pillars")
                                .FontSize(8).FontColor("#757575");
                        });
                        row.ConstantItem(1).Background("#E0E0E0");
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text(kpiCount.ToString())
                                .FontSize(18).Bold().FontColor("#336b58");
                            c.Item().AlignCenter().Text("KPIs")
                                .FontSize(8).FontColor("#757575");
                        });
                    });

                    // Best / worst pillar badges
                    if (best != null && worst != null)
                    {
                        col.Item().PaddingTop(6).Column(b =>
                        {
                            b.Item().Background("#E8F5E9").Padding(5).Row(r =>
                            {
                                r.AutoItem().Text("▲ ").FontSize(8).FontColor("#2E7D32");
                                r.RelativeItem()
                                    .Text($"{Shorten(best.Name, 22)} ({best.Value:F0}%)")
                                    .FontSize(8).FontColor("#1B5E20");
                            });
                            b.Item().PaddingTop(3).Background("#FDECEA").Padding(5).Row(r =>
                            {
                                r.AutoItem().Text("▼ ").FontSize(8).FontColor("#C62828");
                                r.RelativeItem()
                                    .Text($"{Shorten(worst.Name, 22)} ({worst.Value:F0}%)")
                                    .FontSize(8).FontColor("#B71C1C");
                            });
                        });
                    }
                });
        }

        /// <summary>Renders the donut / gauge on an SKCanvas.</summary>
        static void PaintDonut(SKCanvas canvas, Size size, float score)
        {
            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float outerR = Math.Min(cx, cy) - 8f;
            float thick = outerR * 0.30f;
            float mid = outerR - thick / 2f;

            var rect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);

            // Background track
            using var bgPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = SKColor.Parse("#EEF5F1"),
                IsAntialias = true
            };
            canvas.DrawOval(rect, bgPaint);

            // Score arc
            using var arcPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = GetColor(score),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawArc(rect, -90f, 360f * score / 100f, false, arcPaint);

            // Inner shadow ring
            using var shadowPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = SKColor.Parse("#D0E8DC"),
                IsAntialias = true
            };
            canvas.DrawOval(
                new SKRect(cx - mid + thick / 2f + 2, cy - mid + thick / 2f + 2,
                           cx + mid - thick / 2f - 2, cy + mid - thick / 2f - 2),
                shadowPaint);

            // Center: score value
            using var bigTxt = new SKPaint
            {
                Color = GetColor(score),
                TextSize = 26,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText($"{score:F1}%", cx, cy + 9, bigTxt);

            // Center: sub-label
            using var subTxt = new SKPaint
            {
                Color = SKColor.Parse("#9E9E9E"),
                TextSize = 8,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText("city progress", cx, cy + 21, subTxt);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — Pillar Radar / Spider Card
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderPillarRadarCard(IContainer container, List<KpiChartItem> pillars)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(10)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text("Pillar Performance Radar")
                        .FontSize(10).Bold().FontColor("#12352f");

                    col.Item().Height(240).Canvas((canvas, size) =>
                        PaintSpiderChart(canvas, size, pillars));
                });
        }

        /// <summary>Renders a filled spider/radar chart onto an SKCanvas.</summary>
        static void PaintSpiderChart(SKCanvas canvas, Size size, List<KpiChartItem> pillars)
        {
            int n = pillars.Count;
            if (n < 3) return;

            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float radius = Math.Min(cx, cy) - 42f;

            // ── concentric grid rings ────────────────────────────────────────────
            using var ringPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse("#DDE8E3"),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            using var ringLblPaint = new SKPaint
            {
                Color = SKColor.Parse("#C0C0C0"),
                TextSize = 7,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left
            };

            for (int r = 1; r <= 4; r++)
            {
                float rr = radius * r / 4f;
                var pts = BuildRadarPoints(cx, cy, rr, n);
                var path = BuildPath(pts);
                canvas.DrawPath(path, ringPaint);
                canvas.DrawText($"{r * 25}", cx + rr + 2, cy - 2, ringLblPaint);
            }

            // ── spoke axes ──────────────────────────────────────────────────────
            using var axisPaint = new SKPaint
            {
                Color = SKColor.Parse("#C8D8D0"),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius, i, n);
                canvas.DrawLine(cx, cy, tip.X, tip.Y, axisPaint);
            }

            // ── data polygon ─────────────────────────────────────────────────────
            var dataPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                if (i == 0) dataPath.MoveTo(pt);
                else dataPath.LineTo(pt);
            }
            dataPath.Close();

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse("#336b58").WithAlpha(55),
                IsAntialias = true
            };
            using var edgePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = SKColor.Parse("#2E7D32"),
                IsAntialias = true
            };
            canvas.DrawPath(dataPath, fillPaint);
            canvas.DrawPath(dataPath, edgePaint);

            // ── data-point dots ──────────────────────────────────────────────────
            using var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse("#2E7D32"),
                IsAntialias = true
            };
            using var dotBorder = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f,
                Color = new SKColor(),
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotPaint);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotBorder);
            }

            // ── axis labels ──────────────────────────────────────────────────────
            using var lblPaint = new SKPaint
            {
                Color = SKColor.Parse("#2c3e35"),
                TextSize = 8f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            using var valPaint = new SKPaint
            {
                Color = SKColor.Parse("#558a70"),
                TextSize = 7f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius + 26f, i, n);
                canvas.DrawText(
                    Shorten(pillars[i].ShortName ?? pillars[i].Name, 10),
                    tip.X, tip.Y + 3f, lblPaint);
            }
        }

        // ── Radar geometry helpers ───────────────────────────────────────────────────

        static SKPoint RadarPt(float cx, float cy, float r, int i, int n)
        {
            float angle = (-90f + 360f * i / n) * (float)Math.PI / 180f;
            return new SKPoint(cx + r * (float)Math.Cos(angle),
                               cy + r * (float)Math.Sin(angle));
        }

        static SKPoint[] BuildRadarPoints(float cx, float cy, float r, int n)
            => Enumerable.Range(0, n).Select(i => RadarPt(cx, cy, r, i, n)).ToArray();

        static SKPath BuildPath(SKPoint[] pts)
        {
            var p = new SKPath();
            if (pts.Length == 0) return p;
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.Close();
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — KPI Distribution Band  (4 stat cards)
        // ─────────────────────────────────────────────────────────────────────────────

        static void RenderKpiDistributionBand(
            IContainer container, int total, int green, int amber, int red)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(10)
                .Column(col =>
                {
                    col.Item()
                        .Text("KPI Performance Distribution")
                        .FontSize(9).Bold().FontColor("#12352f");

                    col.Item().PaddingTop(7).Row(row =>
                    {
                        DashboardStatCard(row.RelativeItem(),
                            green.ToString(), "Performing ≥70%", "#E8F5E9", "#2E7D32");
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            amber.ToString(), "Developing 40–69%", "#FFF8E1", "#E65100");
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            red.ToString(), "Needs Improvement", "#FDECEA", "#C62828");
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            total.ToString(), "Total KPIs", "#EEF5F1", "#12352f");
                    });
                });
        }

        /// <summary>Single coloured stat card used inside the distribution band.</summary>
        static void DashboardStatCard(
            IContainer container, string value, string label, string bg, string textColor)
        {
            container
                .Background(bg)
                .Padding(8)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text(value).FontSize(20).Bold().FontColor(textColor);
                    col.Item().AlignCenter()
                        .Text(label).FontSize(7).FontColor(textColor);
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — KPI Sparkline (gradient area chart)
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderKpiSparklineCard(IContainer container, List<KpiChartItem> kpis)
        {
            float avg = (float)kpis.Average(k => k.Value ?? 0);

            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Row(hdr =>
                    {
                        hdr.RelativeItem()
                            .Text("KPI Overview — All Indicators (sorted high → low)")
                            .FontSize(9).Bold().FontColor("#12352f");
                        hdr.AutoItem()
                            .Text($"Avg: {avg:F1}%")
                            .FontSize(9).Bold().FontColor(GetBarColor(avg));
                    });

                    col.Item().PaddingTop(6).Height(78).Canvas((canvas, size) =>
                        PaintKpiSparkline(canvas, size, kpis));
                });
        }

        /// <summary>
        /// Gradient area sparkline for up to 107 KPIs, sorted descending.
        /// Includes dashed 70 % threshold line.
        /// </summary>
        static void PaintKpiSparkline(SKCanvas canvas, Size size, List<KpiChartItem> kpis)
        {
            var data = kpis.OrderByDescending(k => k.Value).ToList();
            int n = data.Count;
            if (n < 2) return;

            const float lp = 28f, bp = 12f, tp = 4f;
            float w = size.Width - lp;
            float h = size.Height - bp - tp;
            float sx = w / (n - 1);

            // Grid lines
            using var gp = new SKPaint { Color = SKColor.Parse("#F0F4F1"), StrokeWidth = 0.7f };
            using var gl = new SKPaint
            {
                Color = SKColor.Parse("#C0C0C0"),
                TextSize = 7,
                TextAlign = SKTextAlign.Right,
                IsAntialias = true
            };
            foreach (float m in new[] { 25f, 50f, 75f, 100f })
            {
                float y = tp + h - m / 100f * h;
                canvas.DrawLine(lp, y, size.Width, y, gp);
                canvas.DrawText($"{(int)m}", lp - 3, y + 3, gl);
            }

            // Gradient fill under line
            var fPath = new SKPath();
            fPath.MoveTo(lp, tp + h);
            fPath.LineTo(lp, tp + h - (float)(data[0].Value ?? 0) / 100f * h);
            for (int i = 1; i < n; i++)
                fPath.LineTo(lp + i * sx, tp + h - (float)(data[i].Value ?? 0) / 100f * h);
            fPath.LineTo(lp + (n - 1) * sx, tp + h);
            fPath.Close();

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, tp), new SKPoint(0, tp + h),
                new[] { SKColor.Parse("#336b58").WithAlpha(95),
                SKColor.Parse("#336b58").WithAlpha(8) },
                null, SKShaderTileMode.Clamp);
            using var fp = new SKPaint { Shader = shader, Style = SKPaintStyle.Fill };
            canvas.DrawPath(fPath, fp);

            // Line
            var lPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float x = lp + i * sx;
                float y = tp + h - (float)(data[i].Value ?? 0) / 100f * h;
                if (i == 0) lPath.MoveTo(x, y); else lPath.LineTo(x, y);
            }
            using var lPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.6f,
                Color = SKColor.Parse("#336b58"),
                IsAntialias = true
            };
            canvas.DrawPath(lPath, lPaint);

            // Dashed 70 % threshold
            float y70 = tp + h - 0.70f * h;
            using var thPaint = new SKPaint
            {
                Color = SKColor.Parse("#2E7D32").WithAlpha(140),
                StrokeWidth = 0.9f,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
            };
            canvas.DrawLine(lp, y70, size.Width, y70, thPaint);

            using var thLbl = new SKPaint
            {
                Color = SKColor.Parse("#2E7D32"),
                TextSize = 7,
                IsAntialias = true
            };
            canvas.DrawText("70%", size.Width - 24, y70 - 2, thLbl);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        //  REDESIGNED KPI DASHBOARD + PILLAR OVERVIEW
        //  Drop-in replacements for KpiDashboardPage / DrawKpiLineChart /
        //  PillarLineChartPage / DrawPillarsRadialChart
        // ═══════════════════════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────────────────────
        //  KPI DASHBOARD PAGE  ·  coloured bar chart layout
        // ─────────────────────────────────────────────────────────────────────────────

        void KpiDashboardPage(IContainer container, List<KpiChartItem> kpis)
        {
            var data = kpis.Where(x => x.Value.HasValue).Take(107).ToList();
            if (!data.Any()) return;

            // sort high→low so it looks intentional, not random
            data = data.OrderByDescending(x => x.Value).ToList();

            int total = data.Count;
            int green = data.Count(x => x.Value >= 70);
            int amber = data.Count(x => x.Value >= 40 && x.Value < 70);
            int red = data.Count(x => x.Value < 40);
            float avg = (float)data.Average(x => x.Value ?? 0);

            // groups of 20 per chart row
            var groups = data
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 18)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(8);

                // ── summary band ──────────────────────────────────────────────────
                col.Item().Height(70).Element(x =>
                    DrawKpiSummaryBand(x, total, green, amber, red, avg));

                // ── bar-chart groups ──────────────────────────────────────────────
                foreach (var group in groups.Where(g => g.Any()))
                    col.Item().Height(148).Element(x => DrawKpiBarChart(x, group));
            });
        }

        // ── KPI summary band (stat cards row) ───────────────────────────────────────

        static void DrawKpiSummaryBand(
            IContainer container,
            int total, int green, int amber, int red, float avg)
        {
            container
                .Background("#12352f")
                .Padding(10)
                .Row(row =>
                {
                    KpiStatPill(row.RelativeItem(), total.ToString(),
                        "Total KPIs", "#4CAF50", "#4CAF5025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), green.ToString(),
                        "Performing ≥70 %", "#4CAF50", "#4CAF5025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), amber.ToString(),
                        "Developing 40–69 %", "#FFC107", "#FFC10725");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), red.ToString(),
                        "Needs Improvement", "#EF5350", "#EF535025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), $"{avg:F1}%",
                        "Average Score", GetBarColor(avg) == "#2E7D32" ? "#4CAF50"
                                       : GetBarColor(avg) == "#F9A825" ? "#FFC107" : "#EF5350",
                        "#4CAF5025");
                });
        }

        static void KpiStatPill(
            IContainer container, string value, string label, string valueColor, string bg)
        {
            container
                .Background(bg)
                .Padding(6)
                .Column(c =>
                {
                    c.Item().AlignCenter()
                        .Text(value).FontSize(15).Bold().FontColor(valueColor);
                    c.Item().AlignCenter()
                        .Text(label).FontSize(6.5f).FontColor("#FFFFFFBB");
                });
        }

        // ── KPI bar chart (one group of ≤20 KPIs) ───────────────────────────────────

        void DrawKpiBarChart(IContainer container, List<KpiChartItem> data)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#DDE8E3")
                .Canvas((canvas, size) =>
                {
                    if (!data.Any()) return;

                    const float lp = 8f;   // left pad
                    const float rp = 8f;   // right pad
                    const float tp = 22f;  // top pad (room for value label)
                    const float bp = 26f;  // bottom pad (room for KPI code label)

                    float chartW = size.Width - lp - rp;
                    float chartH = size.Height - tp - bp;

                    int n = data.Count;
                    float barW = chartW / n;
                    float innerW = barW * 0.62f;
                    float barGap = (barW - innerW) / 2f;

                    // ── background grid ──────────────────────────────────────────
                    using var gridPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#F2F7F4"),
                        StrokeWidth = 0.6f,
                        IsAntialias = false
                    };
                    using var gridLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#B0BEC5"),
                        TextSize = 7f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Left
                    };

                    foreach (float pct in new[] { 25f, 50f, 75f, 100f })
                    {
                        float gy = tp + chartH - pct / 100f * chartH;
                        canvas.DrawLine(lp, gy, lp + chartW, gy, gridPaint);
                        canvas.DrawText($"{(int)pct}", lp + 2, gy - 2, gridLblPaint);
                    }

                    // dashed 70 % threshold
                    float y70 = tp + chartH - 0.70f * chartH;
                    using var threshPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#2E7D32").WithAlpha(100),
                        StrokeWidth = 0.9f,
                        PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0),
                        IsAntialias = true
                    };
                    canvas.DrawLine(lp, y70, lp + chartW, y70, threshPaint);

                    // ── bars ─────────────────────────────────────────────────────
                    using var valLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#37474F"),
                        TextSize = 6.5f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };
                    using var codeLblPaint = new SKPaint
                    {
                        TextSize = 6.5f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };

                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value ?? 0);
                        float bx = lp + i * barW + barGap;
                        float bh = v / 100f * chartH;
                        float by = tp + chartH - bh;

                        SKColor barColor = GetColor(v);
                        SKColor barLight = barColor.WithAlpha(35);

                        // bar background (ghost)
                        using var ghostPaint = new SKPaint
                        {
                            Color = barLight,
                            IsAntialias = true
                        };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, tp, bx + innerW, tp + chartH), 2, 2),
                            ghostPaint);

                        // filled bar with vertical gradient
                        var barRect = new SKRect(bx, by, bx + innerW, tp + chartH);
                        using var shader = SKShader.CreateLinearGradient(
                            new SKPoint(0, by),
                            new SKPoint(0, tp + chartH),
                            new[] { barColor, barColor.WithAlpha(180) },
                            null,
                            SKShaderTileMode.Clamp);
                        using var barPaint = new SKPaint { Shader = shader, IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(barRect, 2, 2), barPaint);

                        // top cap accent line
                        using var capPaint = new SKPaint
                        {
                            Color = barColor,
                            StrokeWidth = 2.5f,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        canvas.DrawLine(bx + 1, by, bx + innerW - 1, by, capPaint);

                        // value label above bar
                        float valueLabelY = by - 3f;
                        if (valueLabelY < tp + 8) valueLabelY = by + 10f;
                        valLblPaint.Color = barColor;
                        canvas.DrawText($"{v:F0}", bx + innerW / 2f, valueLabelY, valLblPaint);

                        // code label below chart
                        codeLblPaint.Color = SKColor.Parse("#546E7A");
                        canvas.DrawText(
                            Shorten(data[i].ShortName ?? "", 5),
                            bx + innerW / 2f,
                            size.Height - 6f,
                            codeLblPaint);
                    }
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  PILLAR OVERVIEW PAGE  ·  redesigned horizontal bar layout + ring chart
        // ─────────────────────────────────────────────────────────────────────────────

        void PillarLineChartPage(IContainer container, List<KpiChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).Take(14).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);
            var best = data.OrderByDescending(x => x.Value).First();
            var worst = data.OrderBy(x => x.Value).First();

            container.Padding(16).Column(col =>
            {
                col.Spacing(10);

                // ── two-column layout: ring chart (left) + bar list (right) ──────
                col.Item().Height(360).Row(row =>
                {
                    // Left: radial ring chart
                    row.RelativeItem(5).Element(x => DrawPillarsRadialChart(x, data));

                    row.ConstantItem(12);

                    // Right: horizontal bar list
                    row.RelativeItem(6).Element(x =>
                        DrawPillarHorizontalBars(x, data));
                });

                // ── bottom: avg score + best/worst ───────────────────────────────
                col.Item().Element(x =>
                    DrawPillarFooterBand(x, avg, best, worst));
            });
        }

        // ── horizontal bar list for pillars ─────────────────────────────────────────

        static void DrawPillarHorizontalBars(IContainer container, List<KpiChartItem> data)
        {
            var sorted = data.OrderByDescending(x => x.Value).ToList();

            container
                .Background(Colors.White)
                .Border(1).BorderColor("#DDE8E3")
                .Padding(14)
                .Column(col =>
                {
                    col.Item().PaddingBottom(8)
                        .Text("Pillar Scores").FontSize(11).Bold().FontColor("#12352f");

                    col.Spacing(6);

                    foreach (var item in sorted)
                    {
                        float v = (float)(item.Value ?? 0);
                        var color = GetBarColor(v);

                        col.Item().Row(row =>
                        {
                            // Pillar label
                            row.ConstantItem(102).AlignMiddle()
                                .Text(Shorten(item.Name ?? item.ShortName ?? "—", 18))
                                .FontSize(8).FontColor("#37474F");

                            // Bar track
                            row.RelativeItem().AlignMiddle().Height(13)
                                .Background("#F0F4F1")
                                .Canvas((canvas, size) =>
                                {
                                    // filled portion with gradient
                                    float fillW = size.Width * v / 100f;
                                    SKColor barC = SKColor.Parse(color);

                                    using var shader = SKShader.CreateLinearGradient(
                                        new SKPoint(0, 0),
                                        new SKPoint(fillW, 0),
                                        new[] { barC.WithAlpha(210), barC },
                                        null,
                                        SKShaderTileMode.Clamp);
                                    using var fp = new SKPaint
                                    { Shader = shader, IsAntialias = true };
                                    canvas.DrawRoundRect(
                                        new SKRoundRect(
                                            new SKRect(0, 0, fillW, size.Height), 3, 3), fp);
                                });

                            // Score badge
                            row.ConstantItem(38).AlignMiddle().AlignRight()
                                .Text($"{v:F1}%")
                                .FontSize(8).Bold().FontColor(color);
                        });
                    }
                });
        }

        // ── footer band: avg + best + worst ─────────────────────────────────────────

        static void DrawPillarFooterBand(
            IContainer container, float avg, KpiChartItem best, KpiChartItem worst)
        {
            container.Row(row =>
            {
                // Average score
                row.RelativeItem(2)
                    .Background("#12352f")
                    .Padding(12)
                    .Column(c =>
                    {
                        c.Item().AlignCenter()
                            .Text("Average Score").FontSize(9).FontColor("#A5D6A7");
                        c.Item().AlignCenter()
                            .Text($"{avg:F1}%")
                            .FontSize(22).Bold()
                            .FontColor(GetBarColor(avg) == "#2E7D32" ? "#66BB6A"
                                     : GetBarColor(avg) == "#F9A825" ? "#FFD54F" : "#EF5350");
                    });

                row.ConstantItem(6);

                // Best pillar
                row.RelativeItem(3)
                    .Background("#E8F5E9")
                    .Border(1).BorderColor("#C8E6C9")
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background("#2E7D32").Padding(3)
                                .Text("▲ BEST").FontSize(7).Bold().FontColor(Colors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(best.Name ?? "—", 26))
                                .FontSize(9).Bold().FontColor("#1B5E20");
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{best.Value:F1}%").FontSize(16).Bold().FontColor("#2E7D32");
                    });

                row.ConstantItem(6);

                // Worst pillar
                row.RelativeItem(3)
                    .Background("#FDECEA")
                    .Border(1).BorderColor("#FFCDD2")
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background("#C62828").Padding(3)
                                .Text("▼ LOWEST").FontSize(7).Bold().FontColor(Colors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(worst.Name ?? "—", 26))
                                .FontSize(9).Bold().FontColor("#B71C1C");
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{worst.Value:F1}%").FontSize(16).Bold().FontColor("#C62828");
                    });
            });
        }

        // ── radial ring chart (left panel) ──────────────────────────────────────────

        void DrawPillarsRadialChart(IContainer container, List<KpiChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).Take(14).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);

            container
                .Background(Colors.White)
                .Border(1).BorderColor("#DDE8E3")
                .Canvas((canvas, size) =>
                {
                    float cx = size.Width / 2f;
                    float cy = size.Height / 2f;

                    // Use concentric rings: outermost = first pillar
                    int n = data.Count;
                    float maxRadius = Math.Min(cx, cy) - 18f;
                    float minRadius = maxRadius * 0.28f;
                    float ringStep = (maxRadius - minRadius) / n;
                    float ringThick = ringStep * 0.68f;

                    // Chart title
                    using var titlePaint = new SKPaint
                    {
                        Color = SKColor.Parse("#12352f"),
                        TextSize = 10f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        FakeBoldText = true
                    };
                    canvas.DrawText("Pillar Performance", cx, 14f, titlePaint);

                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value ?? 0);
                        float r = maxRadius - i * ringStep;
                        float mid = r - ringThick / 2f;

                        var rect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);

                        SKColor barCol = GetColor(v);

                        // Track ring
                        using var trackPaint = new SKPaint
                        {
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = ringThick,
                            Color = barCol.WithAlpha(22),
                            IsAntialias = true
                        };
                        canvas.DrawOval(rect, trackPaint);

                        // Filled arc
                        using var arcPaint = new SKPaint
                        {
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = ringThick,
                            Color = barCol,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        float sweep = 360f * v / 100f;
                        canvas.DrawArc(rect, -90f, sweep, false, arcPaint);

                        // Label at end of arc
                        float labelAngle = (-90f + sweep) * (float)Math.PI / 180f;
                        float labelR = mid + ringThick / 2f + 6f;
                        float lx = cx + labelR * (float)Math.Cos(labelAngle);
                        float ly = cy + labelR * (float)Math.Sin(labelAngle);

                        // dot at arc end
                        using var dotPaint = new SKPaint
                        {
                            Color = barCol,
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawCircle(
                            cx + mid * (float)Math.Cos(labelAngle),
                            cy + mid * (float)Math.Sin(labelAngle),
                            ringThick / 2f + 1.5f, dotPaint);
                    }

                    // ── centre: average score ──────────────────────────────────
                    using var circleFill = new SKPaint
                    {
                        Color = SKColor.Parse("#12352f"),
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    float cr = minRadius - ringStep * 0.6f;
                    canvas.DrawCircle(cx, cy, cr, circleFill);

                    using var circleRing = new SKPaint
                    {
                        Color = GetColor(avg).WithAlpha(180),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2f,
                        IsAntialias = true
                    };
                    canvas.DrawCircle(cx, cy, cr, circleRing);

                    using var avgNumPaint = new SKPaint
                    {
                        Color = GetColor(avg),
                        TextSize = cr * 0.60f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        FakeBoldText = true
                    };
                    canvas.DrawText($"{avg:F0}", cx, cy + avgNumPaint.TextSize * 0.36f, avgNumPaint);

                    using var avgLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#A5D6A7"),
                        TextSize = cr * 0.26f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };
                    canvas.DrawText("avg%", cx, cy + avgNumPaint.TextSize * 0.36f + avgLblPaint.TextSize + 1f, avgLblPaint);

                    // ── legend on the right side ───────────────────────────────
                    float legendX = cx + Math.Min(cx, cy) + 2f;  // just outside chart — won't fit; draw below instead
                                                                 // (legend is in the horizontal bar panel on the right; no need to repeat here)
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  HEADERS / FOOTERS
        // ─────────────────────────────────────────────────────────────────────────────

        void CityComposeHeader(
            IContainer container,
            AiCitySummeryDto data,
            UserRole userRole,
            string? pillarName)
        {
            container.Column(column =>
            {
                column.Item().Background("#12352f").Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("City Analysis Report")
                            .FontSize(16).FontColor(Colors.White);
                    });
                    row.ConstantItem(150).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("Generated").FontSize(9).FontColor("#a5a8ad");
                        col.Item().AlignRight().Text(DateTime.Now.ToString("MMM dd, yyyy"))
                            .FontSize(10).Bold().FontColor(Colors.White);
                    });
                });

                column.Item().Background("#336b58").Padding(12).Column(col =>
                {
                    string title = string.IsNullOrEmpty(pillarName) ? data.CityName : pillarName;
                    col.Item().Text(title).FontSize(22).Bold().FontColor(Colors.White);
                    col.Item().PaddingTop(3)
                        .Text($"{data.CityName}, {data.State}, {data.Country} | Data Year: {data.ScoringYear}")
                        .FontSize(10).FontColor("#E0E0E0");
                });
            });
        }

        void PillarComposeHeader(IContainer container, AiCityPillarReponse data)
        {
            container.Column(column =>
            {
                column.Item().Background("#12352f").Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Pillar Analysis Report").FontSize(16).FontColor(Colors.White);
                    });
                    row.ConstantItem(150).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("Generated").FontSize(9).FontColor("#a5a8ad");
                        col.Item().AlignRight().Text(DateTime.Now.ToString("MMM dd, yyyy"))
                            .FontSize(10).Bold().FontColor(Colors.White);
                    });
                });

                column.Item().Background("#336b58").Padding(12).Column(col =>
                {
                    col.Item().Text(data.PillarName).FontSize(22).Bold().FontColor(Colors.White);
                    col.Item().PaddingTop(3)
                        .Text($"{data.CityName}, {data.State}, {data.Country} | Data Year: {data.AIDataYear}")
                        .FontSize(10).FontColor("#E0E0E0");
                });
            });
        }

        static void PillarComposeFooter(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Page "); text.CurrentPageNumber();
                        text.Span(" of "); text.TotalPages();
                    });
                    col.Item().PaddingTop(5).AlignCenter()
                        .Text("City Assessment Platform").FontSize(8).FontColor("#9E9E9E");
                });
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  CONTENT SECTIONS
        // ─────────────────────────────────────────────────────────────────────────────

        void CitySummeryComposeContent(
            IContainer container, AiCitySummeryDto data, UserRole userRole)
        {
            container.PaddingTop(4).Column(column =>
            {
                var random = new AiCityPillarReponse
                {
                    EvaluatorProgress = data.EvaluatorProgress,
                    Discrepancy = data.Discrepancy,
                    AIDataYear = data.ScoringYear,
                    AIProgress = data.AIProgress
                };
                column.Item().PaddingTop(10).Element(c => PillarProgressSection(c, random, userRole));

                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", data.EvidenceSummary, "#163329"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Cross-Pillar Patterns", data.CrossPillarPatterns, "#6e9688"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Institutional Capacity", data.InstitutionalCapacity, "#0d8057"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Equity Assessment", data.EquityAssessment, "#a4bab2"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Sustainability Outlook", data.SustainabilityOutlook, "#373d3b"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Strategic Recommendations", data.StrategicRecommendations, "#2e9975"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Data Transparency Note", data.DataTransparencyNote, "#63a68f"));
            });
        }

        void PillarComposeContent(
            IContainer container, AiCityPillarReponse data, UserRole userRole)
        {
            container.PaddingTop(8).Column(column =>
            {
                column.Item().PaddingTop(10).Element(c => PillarProgressSection(c, data, userRole));

                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", data.EvidenceSummary, "#163329"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Red Flags", data.RedFlags, "#6e9688"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Geographic Equity Note", data.GeographicEquityNote, "#0d8057"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Institutional Assessment", data.InstitutionalAssessment, "#2e9975"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Data Gap Analysis", data.DataGapAnalysis, "#a4bab2"));

                if (data.DataSourceCitations?.Any() == true)
                {
                    column.Item().PageBreak();
                    column.Item().PaddingTop(10).Element(c =>
                        DataSourcesSection(c, data.DataSourceCitations.ToList()));
                }
            });
        }

        void PillarProgressSection(
            IContainer container, AiCityPillarReponse data, UserRole userRole)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#E0E0E0")
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("Progress Metrics")
                        .FontSize(16).Bold().FontColor("#203d33");

                    column.Item().PaddingTop(12).Column(col =>
                    {
                        PillarProgressBar(col, "Score", data.AIProgress, "#58a389");
                        col.Item().PaddingTop(10);
                    });
                });
        }

        void PillarProgressBar(
            ColumnDescriptor column, string label, decimal? percentage, string color)
        {
            float per = (float)(percentage ?? 0);
            column.Item().Row(row =>
            {
                row.ConstantItem(140).Text(label).FontSize(11).FontColor("#424242");

                if (per > 0)
                    row.RelativeItem().PaddingLeft(10).Column(col =>
                    {
                        col.Item().Height(20).Background("#F5F5F5").Row(barRow =>
                        {
                            barRow.RelativeItem(per).Background(color);
                            barRow.RelativeItem(100 - (per >= 100 ? 99.9f : per));
                        });
                    });

                row.ConstantItem(55).AlignRight()
                    .Text($"{percentage:F1}%").FontSize(11).Bold().FontColor(color);
            });
        }

        /// <summary>Generic titled content block with accent bar.</summary>
        static void PillarContentSection(
            IContainer container, string title, string content, string accentColor)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(accentColor);
                    row.RelativeItem().Background("#F5F5F5").Padding(12)
                        .Text(title).FontSize(15).Bold().FontColor("#212121");
                });

                column.Item()
                    .Background(Colors.White)
                    .Border(1).BorderColor("#E0E0E0")
                    .Padding(18)
                    .Text(content)
                    .FontSize(10).LineHeight(1.6f).FontColor("#424242");
            });
        }

        void DataSourcesSection(IContainer container, List<AIDataSourceCitation> sources)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background("#396154");
                    row.RelativeItem().Background("#F5F5F5").Padding(12)
                        .Text("Data Source Citations").FontSize(15).Bold().FontColor("#212121");
                });

                column.Item().PaddingTop(10)
                    .Background(Colors.White).Border(1).BorderColor("#E0E0E0").Padding(15)
                    .Column(col =>
                    {
                        foreach (var source in sources.Take(10))
                        {
                            col.Item().PaddingBottom(15).Column(sourceCol =>
                            {
                                sourceCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(source.SourceName)
                                        .FontSize(11).Bold().FontColor("#2c423b");
                                    row.ConstantItem(100).AlignRight()
                                        .Background(GetSourceTypeBadgeColor(source.SourceType))
                                        .Padding(3)
                                        .Text(source.SourceType).FontSize(8).FontColor(Colors.White);
                                });

                                sourceCol.Item().PaddingTop(4).Row(row =>
                                {
                                    row.AutoItem().Text($"Trust Level: {source.TrustLevel}/7")
                                        .FontSize(9).FontColor("#757575");
                                    row.AutoItem().PaddingLeft(15).Text($"Year: {source.DataYear}")
                                        .FontSize(9).FontColor("#757575");
                                });

                                if (!string.IsNullOrEmpty(source.DataExtract))
                                    sourceCol.Item().PaddingTop(6)
                                        .Text(TruncateText(source.DataExtract, 200))
                                        .FontSize(9).FontColor("#616161").Italic();

                                if (!string.IsNullOrEmpty(source.SourceURL))
                                    sourceCol.Item().PaddingTop(4)
                                        .Text(source.SourceURL).FontSize(8).FontColor("#305246").Underline();
                            });

                            if (source != sources.Last())
                                col.Item().PaddingBottom(10).LineHorizontal(1).LineColor("#EEEEEE");
                        }
                    });
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  COLOR / FORMAT UTILITIES  (all static, reusable across pages)
        // ─────────────────────────────────────────────────────────────────────────────

        static SKColor GetColor(float value)
        {
            if (value >= 70) return SKColor.Parse("#2E7D32");
            if (value >= 40) return SKColor.Parse("#F9A825");
            return SKColor.Parse("#C62828");
        }

        static string GetBarColor(float value)
        {
            if (value >= 80) return "#2E7D32";
            if (value >= 60) return "#F9A825";
            return "#C62828";
        }

        static string GetKpiBarColor(decimal value) => value switch
        {
            >= 70 => "#58a389",
            >= 40 => "#c4a230",
            _ => "#c45c3a"
        };

        static string GetKpiLabelColor(decimal value) => value switch
        {
            >= 70 => "#2c6b52",
            >= 40 => "#8a6e1e",
            _ => "#8a3c26"
        };

        static string GetSourceTypeBadgeColor(string sourceType) => sourceType?.ToLower() switch
        {
            "government" => "#133328",
            "academic" => "#172923",
            "international" => "#4d7d6d",
            "news/ngo" => "#1ec990",
            _ => "#0eeba1"
        };

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Length <= max ? text : text[..max] + "…";
        }

        static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..maxLength] + "...";
        }

        #endregion pdf pillars and city report


    }

    public record KpiChartItem(string ShortName, string Name, decimal? Value);

}
