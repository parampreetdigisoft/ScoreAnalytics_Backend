using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class AssessmentResponseService : IAssessmentResponseService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        public AssessmentResponseService(ApplicationDbContext context, IAppLogger appLogger, Download download)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
        }

        public async Task<List<AssessmentResponse>> GetAllAsync()
        {
            try
            {
                return await _context.AssessmentResponses.ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<AssessmentResponse>();
            }
        }
        public async Task<AssessmentResponse> GetByIdAsync(int id)
        {
            try
            {
                return await _context.AssessmentResponses.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync ", ex);
                return new AssessmentResponse();
            }

        }
        public async Task<AssessmentResponse> AddAsync(AssessmentResponse response)
        {
            try
            {
                _context.AssessmentResponses.Add(response);
                await _context.SaveChangesAsync();
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddAsync", ex);
                return new AssessmentResponse();
            }
        }
        public async Task<AssessmentResponse> UpdateAsync(int id, AssessmentResponse response)
        {
            try
            {
                var existing = await _context.AssessmentResponses.FindAsync(id);
                if (existing == null) return null;
                existing.Score = response.Score;
                existing.Justification = response.Justification;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UpdateAsync", ex);
                return new AssessmentResponse();
            }

        }
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var resp = await _context.AssessmentResponses.FindAsync(id);
                if (resp == null) return false;
                _context.AssessmentResponses.Remove(resp);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure DeleteAsync", ex);
                return false;
            }

        }
        public async Task<ResultResponseDto<string>> SaveAssessment(AddAssessmentDto request)
        {
            try
            {
                var now = DateTime.Now;
                var assessment = await _context.Assessments
                    .Include(x => x.PillarAssessments)
                    .ThenInclude(x => x.Responses)
                    .FirstOrDefaultAsync(x =>
                        x.IsActive && x.UpdatedAt.Year == now.Year &&
                        (x.AssessmentID == request.AssessmentID ||
                         x.UserCityMappingID == request.UserCityMappingID));

                // If no assessment found, create a new one
                if (assessment == null)
                {
                    var ucm = await _context.UserCityMappings
                        .FirstOrDefaultAsync(x => x.UserCityMappingID == request.UserCityMappingID);

                    if (ucm == null)
                        return ResultResponseDto<string>.Failure(new[] { "City is not assigned" });

                    assessment = new Assessment
                    {
                        UserCityMappingID = ucm.UserCityMappingID,
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsActive = true,
                        UserCityMapping = ucm,
                        AssessmentPhase = AssessmentPhase.InProgress
                    };
                    _context.Assessments.Add(assessment);
                }

                if (request.PillarID > 0)
                {
                    var pillarAssessment = assessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == request.PillarID);

                    if (pillarAssessment == null)
                    {
                        // Create new pillar assessment
                        pillarAssessment = new PillarAssessment
                        {
                            PillarID = request.PillarID,
                            Assessment = assessment
                        };
                        assessment.PillarAssessments.Add(pillarAssessment);
                    }

                    var existingResponses = pillarAssessment.Responses.ToList();
                    
                    if (!request.IsAutoSave) // removed if entire assessement is update for all responses
                    {
                        var pillar = await _context.Pillars.OrderByDescending(x => x.DisplayOrder).FirstOrDefaultAsync();
                        assessment.AssessmentPhase = pillar?.PillarID == request.PillarID ? AssessmentPhase.Completed : AssessmentPhase.InProgress;

                        var requestResponseIds = request.Responses
                            .Where(r => r.QuestionID > 0)
                            .Select(r => r.QuestionID)
                            .ToHashSet();

                        var toDeleteList = existingResponses.Where(r => !requestResponseIds.Contains(r.QuestionID));

                        foreach (var existing in toDeleteList)
                        {
                            _context.AssessmentResponses.Remove(existing); // <-- delete instead of unlink
                        }
                    }

                    // ADD or UPDATE responses
                    foreach (var response in request.Responses)
                    {
                        var existing = existingResponses
                            .FirstOrDefault(r => r.ResponseID == response.ResponseID || r.QuestionID == response.QuestionID);

                        if (existing == null && !string.IsNullOrEmpty(response.Justification))
                        {
                            // Add new
                            pillarAssessment.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = response.QuestionOptionID,
                                Justification = response.Justification,
                                Source = response.Source,
                                Score = response.Score
                            });
                        }
                        else
                        {
                            // Update existing
                            existing.QuestionID = response.QuestionID;
                            existing.QuestionOptionID = response.QuestionOptionID;
                            existing.Justification = response.Justification;
                            existing.Score = response.Score;
                            existing.Source = response.Source;
                        }
                    }
                    if (request.IsFinalized)
                    {
                        assessment.AssessmentPhase = AssessmentPhase.Completed;
                    }

                    assessment.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();

                _download.InsertAnalyticalLayerResults();

                return ResultResponseDto<string>.Success("", new[] { "Pillar saved successfully" }, 1);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occurred in SaveAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to save assessment" });
            }
        }

        public async Task<PaginationResponse<GetAssessmentResponseDto>> GetAssessmentResult(GetAssessmentRequestDto request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
                if (user == null) return null;

                var userCityMappingIDs = new List<int>();

                //analyst can search by city and evaluator, admin can search by role and city
                if (user.Role != UserRole.Admin)
                {
                    Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                    {
                        UserRole.Analyst => x=> !x.IsDeleted &&  request.SubUserID.HasValue ? x.UserID == request.SubUserID : x.AssignedByUserId == user.UserID,
                        UserRole.Evaluator=>x=> !x.IsDeleted && x.UserID == request.UserId,
                        _ => x => !x.IsDeleted 
                    };

                    userCityMappingIDs = _context.UserCityMappings
                        .Where(predicate)
                        .Select(x => x.UserCityMappingID).ToList();
                }

                var query =
                    from a in _context.Assessments
                        .Include(q => q.PillarAssessments)
                        .ThenInclude(q => q.Responses)
                    where a.IsActive && a.UpdatedAt.Year == request.UpdatedAt.Year
                          && (!request.CityID.HasValue || a.UserCityMapping.CityID == request.CityID.Value)
                           && (user.Role == UserRole.Admin || userCityMappingIDs.Contains(a.UserCityMappingID))
                    join c in _context.Cities
                        .Where(x => !x.IsDeleted
                                 && (!request.CityID.HasValue || x.CityID == request.CityID.Value))
                        on a.UserCityMapping.CityID equals c.CityID
                    join u in _context.Users
                        .Where(x => !x.IsDeleted
                                 && (!request.Role.HasValue || x.Role == request.Role.Value))
                        on a.UserCityMapping.UserID equals u.UserID
                    join createdBy in _context.Users.Where(x => !x.IsDeleted)
                      on a.UserCityMapping.AssignedByUserId equals createdBy.UserID

                    select new GetAssessmentResponseDto
                    {
                        AssessmentID = a.AssessmentID,
                        UserCityMappingID = a.UserCityMappingID,
                        CreatedAt = a.CreatedAt,
                        CityID = c.CityID,
                        CityName = c.CityName,
                        State = c.State,
                        UserID = u.UserID,
                        UserName = u.FullName,
                        Score = a.PillarAssessments.SelectMany(x => x.Responses)
                                 .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                                 .Sum(r => (int?)r.Score ?? 0),
                        AssignedByUser = createdBy.FullName,
                        AssignedByUserId = createdBy.UserID,
                        AssessmentYear = a.UpdatedAt.Year,
                        AssessmentPhase = a.AssessmentPhase
                    };

                var response = await query.ApplyPaginationAsync(request);

                //var totalScore = _context.Pillars
                //    .Include(p => p.Questions)
                //    .SelectMany(p => p.Questions).Count();

                //foreach (var item in response.Data)
                //{
                //    item.Score = Math.Round((item.Score / (totalScore * 4)) * 100);
                //}

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAssessmentResult", ex);
                return new PaginationResponse<GetAssessmentResponseDto>
                {
                    Data = new List<GetAssessmentResponseDto>(),
                    PageNumber = 1,
                    PageSize = 10,
                    TotalRecords = 0
                };
            }

        }
        public async Task<PaginationResponse<GetAssessmentQuestionResponseDto>> GetAssessmentQuestion(GetAssessmentQuestoinRequestDto request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserID == request.UserId);
                if (user == null) return null;

                var userIDs = new List<int>();
                var query = _context.Assessments
                    .Include(a => a.PillarAssessments)
                    .ThenInclude(pa => pa.Responses)
                        .ThenInclude(r => r.Question)
                            .ThenInclude(q => q.QuestionOptions)
                    .Where(a => a.AssessmentID == request.AssessmentID)
                    .SelectMany(a => a.PillarAssessments)
                    .Where(x => !request.PillarID.HasValue || x.PillarID == request.PillarID.Value)
                    .SelectMany(x => x.Responses)
                    .Select(r => new GetAssessmentQuestionResponseDto
                    {
                        AssessmentID = request.AssessmentID,
                        PillerID = r.PillarAssessment.PillarID,
                        PillarName = r.Question.Pillar.PillarName,
                        QuestoinID = r.QuestionID,
                        Score = r.Score,
                        UserID = user.UserID,
                        Justification = r.Justification,
                        Source = r.Source ?? "",
                        QuestionOptionText = r.Question.QuestionOptions
                            .Where(o => o.OptionID == r.QuestionOptionID)
                            .Select(o => o.OptionText)
                            .FirstOrDefault() ?? string.Empty,
                        QuestionText = r.Question.QuestionText
                    });

                var response = await query.ApplyPaginationAsync(request);

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAssessmentQuestion", ex);
                return new PaginationResponse<GetAssessmentQuestionResponseDto>
                {
                    Data = new List<GetAssessmentQuestionResponseDto>(),
                    PageNumber = 1,
                    PageSize = 10,
                    TotalRecords = 0
                };
            }
        }
        public async Task<ResultResponseDto<string>> ImportAssessmentAsync(IFormFile file, int userID)
        {
            try
            {
                var optionList = _context.QuestionOptions.ToHashSet();
                int recordSaved = 0;

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        foreach (var ws in workbook.Worksheets)
                        {
                            var assessmentResponses = new List<AddAssesmentResponseDto>();

                            // Get hidden values from first question section
                            int userCityMappingID = ws.Cell(10, 10).GetValue<int>(); // after header and description
                            int pillarID = ws.Cell(10, 11).GetValue<int>();

                            int lastRow = ws.LastRowUsed().RowNumber();

                            // Start from first question block (approx row 8)
                            for (int row = 8; row <= lastRow; row += 4)
                            {
                                int questionID = ws.Cell(row+2, 12).GetValue<int?>() ?? 0;
                                int questionOptionID = ws.Cell(row + 2, 13).GetValue<int?>() ?? 0;
                                int responseID = ws.Cell(row + 2, 14).GetValue<int?>() ?? 0;

                                if (questionID == 0)
                                    continue;

                                // Validate city-user mapping
                                if (!_context.UserCityMappings.Any(x => !x.IsDeleted && x.UserID == userID && x.UserCityMappingID == userCityMappingID))
                                {
                                    return ResultResponseDto<string>.Failure(new[] { "Invalid file uploaded" });
                                }

                                // ===== Read from Sheet =====
                                string scoreText = ws.Cell(row, 4).GetString().Trim();          // Row 1 - Score
                                string naText = ws.Cell(row + 1, 4).GetString().Trim();          // Row 2 - N/A or Unknown
                                string comment = ws.Cell(row + 2, 4).GetString().Trim();         // Row 3 - Comment
                                string source = ws.Cell(row + 3, 4).GetString().Trim();          // Row 4 - Source (optional)

                                int? score = null;
                                var options = optionList.Where(x => x.QuestionID == questionID).ToList();

                                // Try to map numeric score
                                if (int.TryParse(scoreText, out int parsedScore))
                                {
                                    if (parsedScore >= 0 && parsedScore <= 4)
                                    {
                                        score = parsedScore;
                                        questionOptionID = options.FirstOrDefault(x => x.ScoreValue == score)?.OptionID ?? 0;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(naText))
                                {
                                    // Check N/A or Unknown option
                                    questionOptionID = options
                                        .FirstOrDefault(x => naText.Equals(x.OptionText, StringComparison.OrdinalIgnoreCase))?.OptionID ?? 0;
                                }

                                // Only save if a valid option was selected
                                if (questionOptionID > 0)
                                {
                                    assessmentResponses.Add(new AddAssesmentResponseDto
                                    {
                                        AssessmentID = 0,
                                        QuestionID = questionID,
                                        ResponseID = responseID,
                                        QuestionOptionID = questionOptionID,
                                        Score = score.HasValue ? (ScoreValue)score.Value : null,
                                        Justification =  comment,
                                        Source = string.IsNullOrWhiteSpace(source) ? null : source
                                    });
                                }
                            }

                            // Save assessment for each pillar
                            
                            var assessment = new AddAssessmentDto
                            {
                                AssessmentID = 0,
                                UserCityMappingID = userCityMappingID,
                                PillarID = pillarID,
                                Responses = assessmentResponses
                            };

                            var response = await SaveAssessment(assessment);
                            if (!response.Succeeded)
                                return response;

                            recordSaved++;
                            
                        }
                    }
                }

                return ResultResponseDto<string>.Success("", new[]
                {
                    recordSaved > 0
                        ? $"{recordSaved} Pillars Assessment saved successfully"
                        : "Please fill the sheet properly before submitting"
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in ImportAssessmentAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to save assessment" });
            }
        }
        
        public async Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(UserCityRequstDto userCityRequstDto)
        {
            try
            {
                var userID = userCityRequstDto.UserID;
                var cityID = userCityRequstDto.CityID;

                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == userID && x.Role != UserRole.CityUser);
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
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == cityID && (x.AssignedByUserId == userID || x.UserID == userID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == cityID && x.UserID == userID,
                    _ => x => !x.IsDeleted && x.CityID == cityID
                };


                // 1. Get all UserCityMapping IDs for the city
                var ucmIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                var pillarAssessments = _context.Assessments
                    .Where(a => ucmIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == userCityRequstDto.UpdatedAt.Year)
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

        public async Task<ResultResponseDto<GetAssessmentHistoryDto>> GetAssessmentProgressHistory(int assessmentID)
        {
            try
            {
                // Fetch assessment with pillars & responses in one query
                var assessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(pa => pa.Responses)
                    .FirstOrDefaultAsync(a => a.AssessmentID == assessmentID);

                if (assessment == null)
                {
                    return ResultResponseDto<GetAssessmentHistoryDto>.Failure(new[] { "Failed to get assessment history" });
                }

                // Get total questions directly (avoid Include if not needed)
                var totalQuestions = await _context.Questions.CountAsync();

                // Calculate answered questions
                var totalAnsweredQuestions = assessment.PillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Count();

                // Calculate score (sum only valid scores <= Four)
                var score = assessment.PillarAssessments
                    .SelectMany(pa => pa.Responses)
                    .Where(r => r.Score.HasValue && r.Score.Value <= ScoreValue.Four)
                    .Sum(r => (int)r.Score!.Value);

                // Build response
                var result = new GetAssessmentHistoryDto
                {
                    AssessmentID = assessmentID,
                    Score = score,
                    TotalAnsPillar = assessment.PillarAssessments.Count,
                    TotalAnsQuestion = totalAnsweredQuestions,
                    TotalQuestion = totalQuestions,
                    CurrentProgress = totalQuestions > 0
                        ? Math.Round((totalAnsweredQuestions / (double)totalQuestions) * 100)
                        : 0
                };

                return ResultResponseDto<GetAssessmentHistoryDto>.Success(result, new[] { "Assessment history fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetAssessmentProgressHistory", ex);
                return ResultResponseDto<GetAssessmentHistoryDto>.Failure(new[] { "Failed to get assessment history" });

            }
        }

        public async Task<List<CityPillarUserHistoryReponseDto>> GetCityPillarHistory(GetCityPillarHistoryRequestDto r)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == r.UserID);

                if (user == null)
                    return new List<CityPillarUserHistoryReponseDto>();

                // Build predicate based on role
                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == r.CityID &&
                                             x.AssignedByUserId == r.UserID ,
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == r.CityID && x.UserID == r.UserID,
                    _ => x => !x.IsDeleted && x.CityID == r.CityID
                };

                // 1. Get all UserCityMapping IDs for the city
                var ucmIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();


                // 2. Get all relevant pillar assessments
                var pillarAssessments = _context.Assessments
                    .Where(a => ucmIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == r.UpdatedAt.Year)
                    .SelectMany(a => a.PillarAssessments)
                    .Where(pa => !r.PillarID.HasValue || pa.PillarID == r.PillarID);

                // 3. Query city pillar + basic data (SQL only, no constant lists)
                var cityPillarQuery =
                    from p in _context.Pillars.Where(x => !r.PillarID.HasValue || x.PillarID == r.PillarID)
                    join pa in pillarAssessments on p.PillarID equals pa.PillarID into paGroup
                    from pa in paGroup.DefaultIfEmpty()
                    select new
                    {
                        p.PillarID,
                        p.PillarName,
                        UserID = pa != null ? pa.Assessment.UserCityMapping.UserID : 0,
                        Responses = pa != null ? pa.Responses : null,  // keep it null, not a new List
                        TotalQuestion = p.Questions.Count()
                    };

                // 4. Materialize and process in memory
                var cityPillarList = (await cityPillarQuery.ToListAsync())
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
                            UserID = validResponses.Any() ? x.UserID : 0,
                            Score = validResponses.Sum(r => (int?)r.Score ?? 0),
                            ScoreCount = validResponses.Count,
                            x.TotalQuestion,
                            AnsQuestion = responses.Count(),
                            HasAnswer = responses.Any()
                        };
                    })
                    .ToList();

                if (!cityPillarList.Any())
                    return new List<CityPillarUserHistoryReponseDto>();

                // Preload user dictionary to avoid N+1 calls
                var userIds = cityPillarList.Select(x => x.UserID).Distinct().ToList();
                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                // 4. Grouping and final aggregation
                var cityPillars = cityPillarList
                    .GroupBy(x => new { x.PillarID, x.PillarName, x.UserID })
                    .Select(g =>
                    {
                        var totalAnsScoreOfPillar = g.Sum(x => x.Score);
                        var scoreCount = g.Sum(x => x.ScoreCount);
                        var ansUserCount = g.Where(x => x.UserID > 0).Select(x => x.UserID).Distinct().Count();
                        var totalQuestionsInPillar = g.Max(x => x.TotalQuestion) * ansUserCount;

                        decimal progress = scoreCount > 0 && ansUserCount > 0
                            ? totalAnsScoreOfPillar * 100m / (scoreCount * 4m * ansUserCount)
                            : 0m;

                        return new CityPillarUserHistoryReponseDto
                        {
                            UserID = g.Key.UserID,
                            PillarID = g.Key.PillarID,
                            PillarName = g.Key.PillarName,
                            FullName = usersDict.TryGetValue(g.Key.UserID, out var name) ? name : "",
                            Score = totalAnsScoreOfPillar,
                            ScoreProgress = progress,
                            AnsPillar = g.Count(x => x.HasAnswer),
                            TotalQuestion = totalQuestionsInPillar,
                            AnsQuestion = g.Sum(x => x.AnsQuestion)
                        };
                    })
                    .OrderByDescending(x => x.PillarName)
                    .ToList();



                return cityPillars;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetCityPillarHistory", ex);
                return new List<CityPillarUserHistoryReponseDto>();
            }
        }

        public async Task<ResultResponseDto<string>> ChangeAssessmentStatus(ChangeAssessmentStatusRequestDto r)
        {
            try
            {
                var assessment = await _context.Assessments.FirstOrDefaultAsync(x=>x.AssessmentID == r.AssessmentID);
                if(assessment != null)
                {
                    assessment.AssessmentPhase = r.AssessmentPhase;

                    _context.Assessments.Update(assessment);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<string>.Success("", new[] { "Assessment Status Changed successfully" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ChangeAssessmentStatus", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to Changed assessment status" });

            }
            return ResultResponseDto<string>.Failure(new[] { "Failed to Changed assessment status" });
        }

        public async Task<ResultResponseDto<string>> TransferAssessment(TransferAssessmentRequestDto r)
        {
            try
            {
                var currentDate = DateTime.Now;

                var transferAssessment = await _context.Assessments
                    .Include(x => x.UserCityMapping)
                    .Include(x => x.PillarAssessments)
                        .ThenInclude(x => x.Responses)
                    .FirstOrDefaultAsync(x => x.AssessmentID == r.AssessmentID);

                if (transferAssessment == null)
                    return ResultResponseDto<string>.Failure(new[] { "Invalid assessment." });

                var cityAssigned = await _context.UserCityMappings
                    .FirstOrDefaultAsync(x => x.CityID == transferAssessment.UserCityMapping.CityID &&
                                              x.UserID == r.TransferToUserID);

                if (cityAssigned == null)
                    return ResultResponseDto<string>.Failure(new[] { "This assessment can’t be imported because the selected user hasn’t been assigned to this city yet." });

                // Load existing assessment for that user/city/year (with pillars/responses)
                var existingAssessment = await _context.Assessments
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(p => p.Responses)
                    .FirstOrDefaultAsync(a => a.UserCityMappingID == cityAssigned.UserCityMappingID &&
                                              a.UpdatedAt.Year == currentDate.Year);

                if (existingAssessment == null)
                {
                    existingAssessment = new Assessment
                    {
                        UserCityMappingID = cityAssigned.UserCityMappingID,
                        CreatedAt = currentDate,
                        UpdatedAt = currentDate,
                        IsActive = true,
                        AssessmentPhase = transferAssessment.AssessmentPhase == AssessmentPhase.Completed ?  transferAssessment.AssessmentPhase: AssessmentPhase.InProgress,
                        PillarAssessments = new List<PillarAssessment>()
                    };

                    _context.Assessments.Add(existingAssessment);
                }
                else
                {
                    existingAssessment.UpdatedAt = currentDate;
                    existingAssessment.AssessmentPhase = transferAssessment.AssessmentPhase == AssessmentPhase.Completed ? transferAssessment.AssessmentPhase : AssessmentPhase.InProgress;
                }

                // Transfer pillar data
                foreach (var pillar in transferAssessment.PillarAssessments)
                {
                    var existingPillar = existingAssessment.PillarAssessments
                        .FirstOrDefault(x => x.PillarID == pillar.PillarID);

                    if (existingPillar == null)
                    {
                        existingPillar = new PillarAssessment
                        {
                            PillarID = pillar.PillarID,
                            Responses = new List<AssessmentResponse>()
                        };
                        existingAssessment.PillarAssessments.Add(existingPillar);
                    }

                    // Add/Update responses
                    foreach (var response in pillar.Responses)
                    {
                        var existingResponse = existingPillar.Responses
                            .FirstOrDefault(rp => rp.QuestionID == response.QuestionID);

                        if (existingResponse == null)
                        {
                            existingPillar.Responses.Add(new AssessmentResponse
                            {
                                QuestionID = response.QuestionID,
                                QuestionOptionID = response.QuestionOptionID,
                                Justification = response.Justification,
                                Score = response.Score
                            });
                        }
                        else
                        {
                            existingResponse.QuestionOptionID = response.QuestionOptionID;
                            existingResponse.Justification = response.Justification;
                            existingResponse.Score = response.Score;
                        }
                    }

                    // Delete responses not present in transferAssessment
                    var transferQuestionIds = pillar.Responses.Select(x => x.QuestionID).ToHashSet();
                    var toDeleteResponses = existingPillar.Responses
                        .Where(x => !transferQuestionIds.Contains(x.QuestionID))
                        .ToList();

                    foreach (var resp in toDeleteResponses)
                    {
                        //existingPillar.Responses.Remove(resp);
                        _context.AssessmentResponses.Remove(resp);
                    }
                }

                // Delete pillars not present in transferAssessment
                var transferPillarIds = transferAssessment.PillarAssessments.Select(x => x.PillarID).ToHashSet();
                var toDeletePillars = existingAssessment.PillarAssessments
                    .Where(x => !transferPillarIds.Contains(x.PillarID))
                    .ToList();

                foreach (var pillar in toDeletePillars)
                {
                    //existingAssessment.PillarAssessments.Remove(pillar);
                    _context.PillarAssessments.Remove(pillar);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Assessment transferred successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in TransferAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "Failed to transfer assessment, please try again later." });
            }
        }

    }
}