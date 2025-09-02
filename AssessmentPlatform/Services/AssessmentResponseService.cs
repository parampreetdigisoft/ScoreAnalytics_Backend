using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AssessmentPlatform.Services
{
    public class AssessmentResponseService : IAssessmentResponseService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public AssessmentResponseService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
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
                var saveResponse = 0;
                var assessment = await _context.Assessments
                    .Include(x => x.PillarAssessments)
                    .ThenInclude(x => x.Responses)
                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        (x.AssessmentID == request.AssessmentID || x.UserCityMappingID == request.UserCityMappingID));

                if (assessment == null)
                {
                    var ucm = await _context.UserCityMappings
                        .FirstOrDefaultAsync(x => x.UserCityMappingID == request.UserCityMappingID);
                    if (ucm == null)
                    {
                        return ResultResponseDto<string>.Failure(new[] { "City is not assigned" });
                    }
                    assessment = new Assessment
                    {
                        UserCityMappingID = ucm.UserCityMappingID,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsActive = true,
                        UserCityMapping = ucm
                    };
                    _context.Assessments.Add(assessment);
                }
                if(request.PillarID > 0 && !assessment.PillarAssessments.Any(x=>x.PillarID == request.PillarID))
                {
                    var newPillarAssessment = new PillarAssessment
                    {
                        PillarID = request.PillarID,
                        AssessmentID = assessment.AssessmentID,
                        Assessment = assessment
                    };
                    foreach (var response in request.Responses)
                    {
                        var r = new AssessmentResponse
                        {
                            QuestionID = response.QuestionID,
                            QuestionOptionID = response.QuestionOptionID,
                            Justification = response.Justification,
                            Score = response.Score,
                        };
                        newPillarAssessment.Responses.Add(r);
                    }
                    assessment.PillarAssessments.Add(newPillarAssessment);
                    assessment.UpdatedAt = DateTime.Now;
                    saveResponse++;
                }
                else
                {
                    return ResultResponseDto<string>.Failure(new[] { "Pillar response is not saved you may provided wrong details" });
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { "Assessment saved successfully" }, 1);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in SaveAssessment", ex);
                return ResultResponseDto<string>.Failure(new[] { "failed to saved assessment" });
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
                    userCityMappingIDs = _context.UserCityMappings
                        .Where(x => !x.IsDeleted
                        && request.SubUserID.HasValue ? x.UserID == request.SubUserID : x.AssignedByUserId == user.UserID // analyst case
                        || (user.Role == UserRole.Evaluator && x.UserID == request.UserId) // for evaluator 
                        )
                        .Select(x => x.UserCityMappingID).ToList();
                }

                var query =
                    from a in _context.Assessments
                        .Include(q => q.PillarAssessments)
                        .ThenInclude(q => q.Responses)
                    where a.IsActive
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
                        AssignedByUserId = createdBy.UserID
                    };

                var response = await query.ApplyPaginationAsync(request);

                var totalScore = _context.Pillars
                    .Include(p => p.Questions)
                    .SelectMany(p => p.Questions).Count();

                foreach (var item in response.Data)
                {
                    item.Score = Math.Round((item.Score / (totalScore * 4)) * 100);
                }

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
                int recordSaved = 0;
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        bool isValidFile = false;
                        foreach (var ws in workbook.Worksheets)
                        {
                            int userCityMappingID = 0;
                            int pillarID = 0;
                            int questionID = 0;
                            var assesmentResponseList = new List<AddAssesmentResponseDto>();
                            int row = 1;
                            while (!IsRowAndNextThreeEmpty(ws, row))
                            {
                                if (ws.Cell(row, 1).GetString() == "UserCityMappingID")
                                {
                                    // Metadata row is next
                                    var metaRow = row + 1;
                                    userCityMappingID = ws.Cell(metaRow, 1).GetValue<int>();

                                    if (!isValidFile)
                                    {
                                        isValidFile = _context.UserCityMappings.Any(x => !x.IsDeleted && x.UserID == userID && x.UserCityMappingID == userCityMappingID);
                                        if (!isValidFile)
                                        {
                                            return ResultResponseDto<string>.Failure(new[] { "Invalid file you uploaded" });
                                        }
                                    }

                                    pillarID = ws.Cell(metaRow, 2).GetValue<int>();
                                    var pillarName = ws.Cell(metaRow, 3).GetString();
                                    questionID = ws.Cell(metaRow, 4).GetValue<int>();
                                    var questionText = ws.Cell(metaRow, 5).GetString();

                                    // Skip until we reach "Answer" row
                                    while (!ws.Cell(row, 2).IsEmpty() && ws.Cell(row, 2).GetString() != "Answer")
                                    {
                                        row++;
                                    }

                                    if (ws.Cell(row, 2).GetString() == "Answer")
                                    {
                                        var ansRow = row;
                                        var optionId = ws.Cell(ansRow, 3).GetValue<int?>();
                                        var score = ws.Cell(ansRow, 4).GetValue<int?>();
                                        var comment = ws.Cell(ansRow, 5).GetString();
                                        if (optionId != null && optionId > 0 && comment != null)
                                        {
                                            assesmentResponseList.Add(new AddAssesmentResponseDto
                                            {
                                                AssessmentID = 0,
                                                QuestionID = questionID,
                                                QuestionOptionID = optionId.GetValueOrDefault(),
                                                Score = score != null ? (ScoreValue)score : null,
                                                Justification = comment
                                            });
                                        }
                                        row++;
                                    }
                                }
                                row++;
                            }
                           if(userCityMappingID > 0 && pillarID > 0)
                           {
                                var assessment = new AddAssessmentDto
                                {
                                    AssessmentID = 0,
                                    UserCityMappingID = userCityMappingID,
                                    PillarID = pillarID,
                                    Responses = assesmentResponseList
                                };

                                var response = await SaveAssessment(assessment);
                                if (!response.Succeeded)
                                {
                                    return response;
                                }
                                recordSaved++;
                           }
                        }
                    }
                }
                return ResultResponseDto<string>.Success("", new[] { recordSaved > 0
                    ? recordSaved + " Pillars Assessment saved successfully"
                    : "Please fill the sheet in sequece to submit the assessment" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ImportAssessmentAsync", ex);
                return ResultResponseDto<string>.Failure(new[] { "failed to saved assessment" });
            }

        }
        private bool IsRowAndNextThreeEmpty(IXLWorksheet ws, int row)
        {
            for (int r = row; r < row + 4; r++)            // current + next 3 rows
            {
                for (int c = 1; c <= 5; c++)               // first 5 columns
                {
                    if (!ws.Cell(r, c).IsEmpty())          // if any cell has value → not empty
                        return false;
                }
            }
            return true;  // all 4 rows × 5 cols are empty
        }
        public async Task<GetCityQuestionHistoryReponseDto> GetCityQuestionHistory(int cityID)
        {
            try
            {


                // 1. Get all UserCityMapping IDs for the city
                var ucmIds = await _context.UserCityMappings
                    .Where(x => x.CityID == cityID && !x.IsDeleted)
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

                // 2. Fetch city-wise pillar/question details in one go
                var cityPillarQuery = from a in _context.Assessments
                                      where ucmIds.Contains(a.UserCityMappingID) && a.IsActive
                                      from pa in a.PillarAssessments
                                      join p in _context.Pillars on pa.PillarID equals p.PillarID
                                      select new
                                      {
                                          p.PillarID,
                                          p.PillarName,
                                          Score = pa.Responses
                                              .Where(r => r.Score.HasValue && (int)r.Score.Value <= (int)ScoreValue.Four)
                                              .Sum(r => (int?)r.Score ?? 0),
                                          TotalQuestion = p.Questions.Count(),
                                          AnsQuestion = pa.Responses.Count()
                                      };

                var cityPillars = await cityPillarQuery
                    .GroupBy(x => new { x.PillarID, x.PillarName })
                    .Select(g => new CityPillarQuestionHistoryReponseDto
                    {
                        PillarID = g.Key.PillarID,
                        PillarName = g.Key.PillarName,
                        Score = g.Sum(x => x.Score),
                        AnsPillar = g.Count(), // number of times pillar answered
                        TotalQuestion = g.Max(x => x.TotalQuestion), // avoid duplicate sum
                        AnsQuestion = g.Sum(x => x.AnsQuestion)
                    })
                    .ToListAsync();

                // 3. Get assessment count in one query
                var assessmentCount = await _context.Assessments
                    .CountAsync(x => ucmIds.Contains(x.UserCityMappingID) && x.IsActive);

                // 4. Total pillars and questions (static across city)
                var pillarStats = await _context.Pillars
                    .Select(p => new { QuestionsCount = p.Questions.Count() })
                    .ToListAsync();
                int totalPillars = pillarStats.Count;
                int totalQuestions = pillarStats.Sum(p => p.QuestionsCount);

                // 5. Final payload
                var payload = new GetCityQuestionHistoryReponseDto
                {
                    CityID = cityID,
                    TotalAssessment = assessmentCount,
                    Score = cityPillars.Sum(x => x.Score),
                    TotalPillar = totalPillars * assessmentCount,
                    TotalAnsPillar = cityPillars.Sum(x => x.AnsPillar),
                    TotalQuestion = totalQuestions,
                    AnsQuestion = cityPillars.Sum(x => x.AnsQuestion),
                    Pillars = cityPillars
                };

                return payload;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetCityQuestionHistory", ex);
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
        }
    }
}