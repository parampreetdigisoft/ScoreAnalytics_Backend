using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
namespace AssessmentPlatform.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public QuestionService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        public async Task<List<Pillar>> GetPillarsAsync()
        {
            try
            {
                return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetPillarsAsync", ex);
                return new List<Pillar>();
            }
        }

        public async Task<PaginationResponse<GetQuestionRespones>> GetQuestionsAsync(GetQuestionRequestDto request)
        {
            try
            {
                var query =
                from q in _context.Questions
                    .Include(q => q.Pillar)
                    .Include(o => o.QuestionOptions)
                where !q.IsDeleted
                   && (!request.PillarID.HasValue || q.PillarID == request.PillarID.Value)
                select new GetQuestionRespones
                {
                    QuestionID = q.QuestionID,
                    QuestionText = q.QuestionText,
                    PillarID = q.PillarID,
                    PillarName = q.Pillar.PillarName,
                    DisplayOrder = q.DisplayOrder,
                    QuestionOptions = q.QuestionOptions.ToList()
                };

                var response = await query.ApplyPaginationAsync(request);

                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsAsync", ex);
                return new PaginationResponse<GetQuestionRespones>();
            }
        }

        public async Task<Question> AddQuestionAsync(Question q)
        {
            try
            {
                _context.Questions.Add(q);
                await _context.SaveChangesAsync();
                return q;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddQuestionAsync", ex);
                return new Question();
            }
        }

        public async Task<Question> EditQuestionAsync(int id, Question q)
        {
            try
            {
                var existing = await _context.Questions.FindAsync(id);
                if (existing == null) return null;
                existing.QuestionText = q.QuestionText;
                existing.PillarID = q.PillarID;
                existing.DisplayOrder = q.DisplayOrder;
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return new Question();
            }
        }

        public async Task<bool> DeleteQuestionAsync(int id)
        {
            try
            {
                var q = await _context.Questions.FindAsync(id);
                if (q == null) return false;

                q.IsDeleted = true;
                _context.Questions.Update(q);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return true;
            }
        }
        public async Task<ResultResponseDto<string>> AddUpdateQuestion(AddUpdateQuestionDto q)
        {
            try
            {
                var pillarQuestions = await _context.Questions
                .Include(x => x.QuestionOptions)
                .Where(x => x.PillarID == q.PillarID)
                .ToListAsync();

                var totalQuestions = pillarQuestions.Count;
                var question =  _context.Questions
                    .Include(x=>x.QuestionOptions)
                    .FirstOrDefault(x => x.QuestionID == q.QuestionID) ?? new Question();
                if (question.QuestionID > 0 && !pillarQuestions.Select(x => x.QuestionID).Contains(q.QuestionID))
                {
                    question.DisplayOrder = totalQuestions + 1;
                }

                // Common properties
                question.IsDeleted = false;
                question.QuestionText = q.QuestionText;
                question.PillarID = q.PillarID;

                // Sync options (Add / Update / Delete)
                var incomingOptions = q.QuestionOptions ?? new List<QuestionOption>();


                foreach (var o in incomingOptions)
                {
                    var option = question.QuestionOptions.FirstOrDefault(x => x.OptionID == o.OptionID && x.OptionID > 0);

                    if (option == null) // new option
                    {
                        option = new QuestionOption
                        {
                            OptionText = o.OptionText,
                            DisplayOrder = (o.ScoreValue ?? -1) + 1,
                            ScoreValue = o.ScoreValue,
                            Question = question
                        };
                        question.QuestionOptions.Add(option);
                    }
                    else // update existing
                    {
                        option.OptionText = o.OptionText;
                        option.DisplayOrder = (o.ScoreValue ?? -1) + 1;
                        option.ScoreValue = o.ScoreValue;
                    }
                }
                // Add default N/A and Unknown only for new question
                if (question.QuestionID == 0)
                {
                    question.DisplayOrder = totalQuestions + 1;

                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 6,
                        OptionText = "N/A",
                        ScoreValue = null
                    });
                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 7,
                        OptionText = "Unknown",
                        ScoreValue = null
                    });
                }

                var optionIdsFromDto = incomingOptions.Select(x => x.OptionID).ToHashSet();
                var optionsToRemove = question.QuestionOptions
                    .Where(x => x.OptionID > 0 && x.ScoreValue != null && !optionIdsFromDto.Contains(x.OptionID))
                    .ToList();

                foreach (var remove in optionsToRemove)
                {
                    _context.QuestionOptions.Remove(remove);
                }

                // Save Question
                if (question.QuestionID > 0)
                    _context.Questions.Update(question);
                else
                    _context.Questions.Add(question);

                await _context.SaveChangesAsync();

                return ResultResponseDto<string>.Success("", new[] { q.QuestionID > 0 ? "Question updated successfully" : "Question saved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddUpdateQuestion", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<string>> AddBulkQuestion(AddBulkQuestionsDto payload)
        {
            try
            {
                var newQuestions = new List<Question>();

                foreach (var q in payload.Questions)
                {
                    var pillarQuestions = await _context.Questions
                        .Where(x => x.PillarID == q.PillarID && !x.IsDeleted)
                        .ToListAsync();
                    if (pillarQuestions.Any(x => x.QuestionText == q.QuestionText && x.PillarID == q.PillarID))
                    {
                        continue;
                    }

                    var totalQuestions = pillarQuestions.Count;

                    var question = new Question
                    {
                        IsDeleted = false,
                        QuestionText = q.QuestionText,
                        PillarID = q.PillarID,
                        DisplayOrder = totalQuestions + 1,
                        QuestionOptions = new List<QuestionOption>()
                    };

                    // Add provided options
                    foreach (var o in q.QuestionOptions)
                    {
                        var option = new QuestionOption
                        {
                            OptionText = o.OptionText,
                            DisplayOrder = (o.ScoreValue ?? -1) + 1,
                            ScoreValue = o.ScoreValue
                        };
                        question.QuestionOptions.Add(option);
                    }

                    // Add default options (N/A & Unknown)
                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 6,
                        OptionText = "N/A",
                        ScoreValue = null
                    });
                    question.QuestionOptions.Add(new QuestionOption
                    {
                        DisplayOrder = 7,
                        OptionText = "Unknown",
                        ScoreValue = null
                    });

                    newQuestions.Add(question);
                }

                // Add all questions in bulk
                if (newQuestions.Count > 0)
                {
                    await _context.Questions.AddRangeAsync(newQuestions);
                    await _context.SaveChangesAsync();
                }

                return ResultResponseDto<string>.Success("", new[] { newQuestions.Count + " Questions imported successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddBulkQuestion", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<GetPillarQuestionByCityRespones>> GetQuestionsByCityIdAsync(CityPillerRequestDto request)
        {
            try
            {
                // Load assessment once (if exists)
                var answeredPillarIds = new List<int>();
                var assessment = await _context.Assessments
                    .Include(x=>x.PillarAssessments).ThenInclude(x=>x.Responses)
                    .Where(a => a.UserCityMappingID == request.UserCityMappingID && a.IsActive)
                    .FirstOrDefaultAsync();
                if (assessment != null)
                {
                    answeredPillarIds = assessment.PillarAssessments
                   .Select(r => r.PillarID)
                   .ToList();
                }
                if(assessment !=null && answeredPillarIds.Count == 14 && !request.PillarID.HasValue)
                {
                    request.PillarID = assessment.PillarAssessments.First().PillarID;
                }

                // Get next unanswered pillar
                var selectPillar = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .Where(p =>  !request.PillarID.HasValue ? !answeredPillarIds.Contains(p.PillarID) : p.PillarID == request.PillarID)
                    .OrderBy(p => p.DisplayOrder)
                    .FirstOrDefaultAsync();

                var summitedPillar = await _context.Pillars
                    .Where(p => !answeredPillarIds.Contains(p.PillarID))
                    .OrderBy(p => p.DisplayOrder)
                    .FirstOrDefaultAsync();

                if (selectPillar == null || selectPillar?.Questions == null)
                {
                    return ResultResponseDto<GetPillarQuestionByCityRespones>.Failure(new[] { "You have submitted assessment for this city" });
                }

                var editAssessmentResponse = new Dictionary<int, AssessmentResponse>();
                if(assessment != null)
                {
                    editAssessmentResponse = assessment.PillarAssessments
                    .Where(a => a.PillarID == request.PillarID)
                    .SelectMany(x => x.Responses)
                    .ToDictionary(x => x.QuestionID);
                }

                // Project questions
                var questions = selectPillar.Questions
                .OrderBy(q => q.DisplayOrder)
                .Select(q =>
                {
                    var questoin = editAssessmentResponse.TryGetValue(q.QuestionID, out var submittedQuestion);
                    submittedQuestion = submittedQuestion ?? new();
                   return new AssessmentQuestionResponseDto
                   {
                        QuestionID = q.QuestionID,
                        QuestionText = q.QuestionText,
                        PillarID = q.PillarID,
                        ResponseID = submittedQuestion.ResponseID,
                        IsSelected = submittedQuestion.QuestionID == q.QuestionID,
                        QuestionOptions = q.QuestionOptions.Select(x => new QuestionOptionDto
                        {
                            DisplayOrder = x.DisplayOrder,
                            OptionID = x.OptionID,
                            QuestionID = x.QuestionID,
                            IsSelected = submittedQuestion.QuestionOptionID == x.OptionID,
                            OptionText = x.OptionText,
                            ScoreValue = x.ScoreValue,
                            Justification = submittedQuestion.Justification
                        }).ToList(),
                    };
                }).ToList();

                var result = new GetPillarQuestionByCityRespones
                {
                    AssessmentID = assessment?.AssessmentID ?? 0,
                    UserCityMappingID = request.UserCityMappingID,
                    PillarName = selectPillar.PillarName,
                    PillarID = selectPillar.PillarID,
                    Description = selectPillar.Description,
                    DisplayOrder = selectPillar.DisplayOrder,
                    SubmittedPillarDisplayOrder = answeredPillarIds.Count == 14 ? 14 : summitedPillar?.DisplayOrder ?? selectPillar.DisplayOrder,
                    Questions = questions
                };
                return ResultResponseDto<GetPillarQuestionByCityRespones>.Success(result, new[] { "get questions successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsByCityIdAsync", ex);
                return ResultResponseDto<GetPillarQuestionByCityRespones>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<Tuple<string, byte[]>> ExportAssessment(int userCityMappingID)
        {
            try
            {

                var fileName = (from m in _context.UserCityMappings
                                join c in _context.Cities on m.CityID equals c.CityID
                                join u in _context.Users on m.AssignedByUserId equals u.UserID
                                where m.UserCityMappingID == userCityMappingID
                                select new
                                {
                                    CityName = c.CityName,
                                    FullName = u.FullName
                                }).FirstOrDefault();

                var sheetName = fileName?.CityName + "_" + fileName?.FullName;


                var assessment = await _context.Assessments
                    .Where(a => a.UserCityMappingID == userCityMappingID && a.IsActive)
                    .FirstOrDefaultAsync();

                // Get next unanswered pillar
                var nextPillars = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();

                var pillarAssessments = _context.Assessments
                    .Include(x=>x.PillarAssessments)
                    .ThenInclude(x=>x.Responses)
                    .Where(a => a.UserCityMappingID == userCityMappingID)
                    .SelectMany(x => x.PillarAssessments).ToList();

                var byteArray = MakePillarSheet(nextPillars, pillarAssessments, userCityMappingID);

                return new(sheetName, byteArray);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportAssessment", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
        private byte[] MakePillarSheet(List<Pillar> pillars, List<PillarAssessment> pillarAssessments, int userCityMappingID)
        {
            using (var workbook = new XLWorkbook())
            {
                foreach (var pillar in pillars)
                {
                    var name = GetValidSheetName(pillar.PillarName);
                    var ws = workbook.Worksheets.Add(name);
                    ws.Columns().Width = 10;
                    ws.Column(1).Width = 18;
                    ws.Column(3).Width = 35;
                    ws.Column(5).Width = 120;
                    var protection = ws.Protect();

                    // allow user to resize (format) columns
                    protection.AllowedElements =
                       XLSheetProtectionElements.FormatColumns |
                       XLSheetProtectionElements.SelectLockedCells |
                       XLSheetProtectionElements.SelectUnlockedCells;

                    var submitedPillarResonse = pillarAssessments
                        .Where(x => x.PillarID == pillar.PillarID)
                        .SelectMany(x=>x.Responses)
                        .ToDictionary(x=>x.QuestionID);

                    int row = 0;
                    var q = pillar?.Questions?.ToList() ?? new();
                    for (var i = 0; i < q.Count; i++)
                    {
                        submitedPillarResonse.TryGetValue(q[i].QuestionID, out var ansQuestion);
                        ansQuestion = ansQuestion ?? new();

                        ++row;
                        ws.Row(row).Style.Font.Bold = true;
                        ws.Cell(row, 1).Value = "UserCityMappingID";
                        ws.Cell(row, 2).Value = "PillarID";
                        ws.Cell(row, 3).Value = "PillarName";
                        ws.Cell(row, 4).Value = "QuestionID";
                        ws.Cell(row, 5).Value = "QuestionText";
                        var headerRange = ws.Range(row, 1, row, 5);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                        headerRange.Style.Font.FontColor = XLColor.White;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        ++row;
                        ws.Cell(row, 1).Value = userCityMappingID;
                        ws.Cell(row, 2).Value = q[i].PillarID;
                        ws.Cell(row, 3).Value = pillar?.PillarName;
                        ws.Cell(row, 4).Value = q[i].QuestionID;
                        ws.Cell(row, 5).Value = q[i].QuestionText;

                        ++row;
                        ws.Row(row).Style.Font.Bold = true;
                        ws.Cell(row, 2).Value = "S.No";
                        ws.Cell(row, 3).Value = "OptionID";
                        ws.Cell(row, 4).Value = "ScoreValue";
                        ws.Cell(row, 5).Value = "OptionText";

                        var opt = q[i].QuestionOptions.ToList() ?? new();
                        for (int j = 0; j < opt.Count; j++)
                        {
                            ++row;
                            ws.Cell(row, 2).Value = j + 1;
                            ws.Cell(row, 3).Value = opt[j].OptionID;
                            ws.Cell(row, 4).Value = opt[j].ScoreValue;
                            ws.Cell(row, 5).Value = opt[j].OptionText;
                        }
                        ++row;
                        ws.Row(row).Style.Font.Bold = true;
                        ws.Cell(row, 2).Value = "Your Assessment";
                        ws.Cell(row, 3).Value = "OptionID";
                        ws.Cell(row, 4).Value = "ScoreValue";
                        ws.Cell(row, 5).Value = "Comment";

                        ++row;
                        var ansHeader = ws.Range(row, 2, row, 2);
                        ansHeader.Style.Fill.BackgroundColor = XLColor.Green;
                        ansHeader.Style.Font.FontColor = XLColor.White;
                        ansHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(row, 3).Style.Protection.SetLocked(false);
                        ws.Cell(row, 4).Style.Protection.SetLocked(false);
                        ws.Cell(row, 5).Style.Protection.SetLocked(false);

                        var dvOptionId = ws.Cell(row, 3).GetDataValidation();
                        var values = opt.OrderBy(x => x.OptionID).ToList();

                        if (values.Any())
                        {
                            dvOptionId.WholeNumber.Between(values.First().OptionID, values.Last().OptionID);
                        }
                        else
                        {
                            dvOptionId.WholeNumber.Between(0, 0);
                            dvOptionId.IgnoreBlanks = true;
                        }

                        var dvScore = ws.Cell(row, 4).GetDataValidation();
                        dvScore.WholeNumber.Between(0, 4);
                        var dvComment = ws.Cell(row, 5).GetDataValidation();
                        dvComment.TextLength.Between(1, 10000);

                        ws.Cell(row, 2).Value = "Answer";
                        ws.Cell(row, 3).Value = ansQuestion.QuestionOptionID == 0 ? "" : ansQuestion.QuestionOptionID;
                        ws.Cell(row, 4).Value = (int?)ansQuestion.Score;
                        ws.Cell(row, 5).Value = ansQuestion.Justification;
                        ++row;
                    }
                }
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return content;
                }
            }
        }
        private string GetValidSheetName(string safeName)
        {
            // Trim to 31 characters
            if (safeName.Length > 31)
                safeName = safeName.Substring(0, 31);

            // Remove illegal characters
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c.ToString(), "");
            }
            safeName = safeName.Replace("[", "").Replace("]", "").Replace(":", "")
                               .Replace("*", "").Replace("?", "").Replace("/", "").Replace("\\", "");

            return safeName;
        }
        public async Task<ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>> GetQuestionsHistoryByPillar(GetCityPillarHistoryRequestDto requestDto)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == requestDto.UserID);

                if (!requestDto.PillarID.HasValue || user == null)
                {
                    return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>.Failure(new[] { "Invalid request" });
                }

                // Fetch pillar + questions
                var pillar = await _context.Pillars
                    .Include(x => x.Questions)
                    .ThenInclude(x=>x.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == requestDto.PillarID.Value);

                if (pillar == null)
                {
                    return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>.Failure(new[] { "Pillar not found" });
                }

                // User mappings (admins can see all in city)
                var userMappings = await _context.UserCityMappings
                    .Where(x => x.CityID == requestDto.CityID
                                && !x.IsDeleted
                                && (x.AssignedByUserId == requestDto.UserID || user.Role == UserRole.Admin))
                    .AsNoTracking()
                    .ToListAsync();

                var mappingIds = userMappings.Select(x => x.UserCityMappingID).ToList();

                var assessments = await _context.Assessments
                    .Include(x=>x.UserCityMapping)
                    .Include(a => a.PillarAssessments
                        .Where(pa => pa.PillarID == requestDto.PillarID)) // allowed
                    .ThenInclude(pa => pa.Responses) // proper navigation include
                    .Where(a => mappingIds.Contains(a.UserCityMappingID) && a.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                var userIds = assessments.Select(x => x.UserCityMapping.UserID);
                // Fetch users dictionary
                var users = await _context.Users
                    .Where(x => userIds.Contains(x.UserID))
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.UserID);

                // Build responses by user
                var responsesByUser = assessments
                    .GroupBy(a => a.UserCityMapping.UserID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(a => a.PillarAssessments)
                              .Where(pa => pa.PillarID == requestDto.PillarID)
                              .SelectMany(pa => pa.Responses)
                              .ToDictionary(r => r.QuestionID)
                    );

                // Build final DTO list
                var pillarResponses = pillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        var userInfos = userIds.Select(uid =>
                        {
                            users.TryGetValue(uid, out var u);
                            responsesByUser.TryGetValue(uid, out var userResponseDict);

                            (userResponseDict ?? new() ).TryGetValue(q.QuestionID, out var response);

                            var optionID= (response ?? new()).QuestionOptionID;
                            var option = q.QuestionOptions.FirstOrDefault(x => x.OptionID == optionID);

                            return new QuestionsByUserInfo
                            {
                                UserID = uid,
                                FullName = u?.FullName ?? string.Empty,
                                Score = response !=null ? (int?)response.Score:null,
                                OptionText = option?.OptionText ?? "",
                                Justification = response?.Justification ?? string.Empty
                            };
                        }).ToList();

                        return new QuestionsByUserPillarsResponsetDto
                        {
                            QuestionID = q.QuestionID,
                            PillarID = q.PillarID,
                            QuestionText = q.QuestionText,
                            DisplayOrder = q.DisplayOrder,
                            Users = userInfos
                        };
                    })
                    .ToList();

                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>.Success(
                    pillarResponses,
                    Array.Empty<string>() // no error message on success
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetQuestionsHistoryByPillar", ex);
                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>.Failure(new[] { "There was an error, please try again later" });
            }
        }

    }
}