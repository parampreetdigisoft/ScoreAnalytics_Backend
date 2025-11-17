using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class PillarService : IPillarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        public PillarService(ApplicationDbContext context, IAppLogger appLogger, Download download)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
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

                if (existing.Weight != pillar.Weight || existing.Reliability != pillar.Reliability)
                {
                    existing.Weight = pillar.Weight;
                    existing.Reliability = pillar.Reliability;
                    _download.InsertAnalyticalLayerResults();
                }
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
                                             x.AssignedByUserId == request.UserID,
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
                    .Where(a => userCityMappingIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == request.UpdatedAt.Year)
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

        public async Task<ResultResponseDto<List<PillarWithQuestionsDto>>> GetPillarsWithQuestions(GetCityPillarHistoryRequestDto request)
        {
            try
            {
                // 1. Validate user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserID == request.UserID);

                if (user == null)
                    return ResultResponseDto<List<PillarWithQuestionsDto>>.Failure(new[] { "Invalid user" });

                // 2. Filter user-city mappings based on role
                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == request.CityID &&
                                             (x.AssignedByUserId == request.UserID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == request.CityID && x.UserID == request.UserID,
                    _ => x => !x.IsDeleted && x.CityID == request.CityID
                };

                var mappingIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                // 3. Get assessments with pillar + responses
                var assessments = await _context.Assessments
                    .Include(a => a.UserCityMapping)
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(pa => pa.Responses)
                    .Where(a => mappingIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == request.UpdatedAt.Year)
                    .AsNoTracking()
                    .ToListAsync();

                // 4. Get pillar list with questions + options
                var pillars = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .Where(p => !request.PillarID.HasValue || p.PillarID == request.PillarID)
                    .OrderBy(p => p.DisplayOrder)
                    .AsNoTracking()
                    .ToListAsync();

                // 5. Preload users dictionary
                var userIds = assessments.Select(a => a.UserCityMapping.UserID).Distinct().ToList();
                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                // 6. Build response
                var result = pillars.Select(p => new PillarWithQuestionsDto
                {
                    PillarID = p.PillarID,
                    PillarName = p.PillarName,
                    DisplayOrder = p.DisplayOrder,
                    TotalQuestions = p.Questions.Count,
                    Questions = p.Questions
                        .OrderBy(q => q.DisplayOrder)
                        .Where(q=>!q.IsDeleted)
                        .Select(q =>
                        {
                            var userAnswers = userIds.Select(uid =>
                            {
                                var paResponses = assessments
                                    .Where(a => a.UserCityMapping.UserID == uid)
                                    .SelectMany(a => a.PillarAssessments)
                                    .Where(pa => pa.PillarID == p.PillarID)
                                    .SelectMany(pa => pa.Responses)
                                    .ToList();

                                var response = paResponses.FirstOrDefault(r => r.QuestionID == q.QuestionID);
                                var option = q.QuestionOptions.FirstOrDefault(o => o.OptionID == response?.QuestionOptionID);

                                return new QuestionUserAnswerDto
                                {
                                    UserID = uid,
                                    FullName = usersDict.TryGetValue(uid, out var name) ? name : "",
                                    Score = (int?)response?.Score,
                                    Justification = response?.Justification ?? "",
                                    OptionText = option?.OptionText ?? ""
                                };
                            }).ToDictionary(x=>x.UserID);

                            return new QuestionWithUserDto
                            {
                                QuestionID = q.QuestionID,
                                QuestionText = q.QuestionText,
                                DisplayOrder = q.DisplayOrder,
                                Users = userAnswers
                            };
                        }).ToList()
                }).ToList();

                return ResultResponseDto<List<PillarWithQuestionsDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPillarsWithQuestions", ex);
                return ResultResponseDto<List<PillarWithQuestionsDto>>.Failure(new[] { "There was an error, please try again later" });
            }
        }

        public async Task<Tuple<string, byte[]>> ExportPillarsHistoryByUserId(GetCityPillarHistoryRequestDto requestDto)
        {
            try
            {
                var response = await GetPillarsWithQuestions(requestDto);
                var city = await _context.Cities.FirstOrDefaultAsync(x => x.CityID == requestDto.CityID);

                if (!response.Succeeded)
                {
                    return new Tuple<string, byte[]>("", Array.Empty<byte>());
                }

                var byteArray = MakePillarSheet(response.Result, city);

                return new("ExportPillarsHistory"+ requestDto.CityID+""+requestDto.PillarID, byteArray);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportPillarsHistoryByUserId", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }

        private byte[] MakePillarSheet(List<PillarWithQuestionsDto> pillars, Models.City? city)
        {
            using (var workbook = new XLWorkbook())
            {
                var name = city == null ? $"{pillars.Count}-Pillars-Result" : city?.CityName+"-"+city?.State+ $"-{pillars.Count}-Pillars-Result";
                var shortName = name.Length > 30 ? name.Substring(0, 30) : name;

                var ws = workbook.Worksheets.Add(shortName);
                ws.Columns().Width = 35;
                ws.Column(1).Width = 6;  // S.NO.
                ws.Column(2).Width = 100;  // Pillar/Question text

                var protection = ws.Protect();
                protection.AllowedElements =
                   XLSheetProtectionElements.FormatColumns |
                   XLSheetProtectionElements.SelectLockedCells |
                   XLSheetProtectionElements.SelectUnlockedCells;

                var names = pillars
                    .SelectMany(p => p.Questions)
                    .SelectMany(q => q.Users.Values)
                    .GroupBy(u => u.UserID)
                    .Select(g => g.First())
                    .ToList();

                int row = 1;
                int pillarCounter = 1;

                foreach (var pillar in pillars)
                {
                    int c = 1;

                    // Header row
                    ws.Cell(row, c++).Value = "S.NO.";
                    ws.Cell(row, c++).Value = "PillarName";
                    foreach (var user in names)
                        ws.Cell(row, c++).Value = user.FullName;

                    var headerRange = ws.Range(row, 1, row, names.Count + 2);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ++row;
                    c = 1;

                    // Pillar row
                    ws.Cell(row, c++).Value = pillarCounter++; // pillar serial number
                    ws.Cell(row, c++).Value = pillar.PillarName;
                    ws.Cell(row, 2).Style.Font.Bold = true;

                    foreach (var user in names)
                    {
                        var score = pillar.Questions
                            .SelectMany(x => x.Users)
                            .Where(x => x.Key == user.UserID)
                            .Sum(x => x.Value.Score) ?? 0;

                        var richText = ws.Cell(row, c++).GetRichText();

                        richText.AddText("Total Score:  ")
                            .SetBold().SetFontColor(XLColor.DarkGray);
                        richText.AddText($"{score}\n")
                            .SetFontColor(XLColor.Black);
                    }

                    row += 2;
                    c = 1;

                    // Question header row
                    ws.Cell(row, c++).Value = "S.NO.";
                    ws.Cell(row, c++).Value = "Questions";
                    foreach (var user in names)
                        ws.Cell(row, c++).Value = user.FullName;

                    var headerQRange = ws.Range(row, 1, row, names.Count + 2);
                    headerQRange.Style.Font.Bold = true;
                    headerQRange.Style.Fill.BackgroundColor = XLColor.TealBlue;
                    headerQRange.Style.Font.FontColor = XLColor.White;
                    headerQRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var q = pillar.Questions;
                    int questionCounter = 1;

                    for (var i = 0; i < q.Count; i++)
                    {
                        ++row;
                        var question = q[i];
                        var usersData = question.Users;

                        c = 1;
                        ws.Cell(row, c++).Value = $"{pillarCounter - 1}.{questionCounter++}";
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        ws.Cell(row, c++).Value = question.QuestionText;
    

                        foreach (var user in names)
                        {
                            usersData.TryGetValue(user.UserID, out var answerDto);
                            answerDto ??= new();

                            var richText = ws.Cell(row, c++).GetRichText();

                            richText.AddText("OptionText: ")
                               .SetBold().SetFontColor(XLColor.DarkRed);
                            richText.AddText($"{answerDto.OptionText ?? "-"}\n")
                                .SetFontColor(XLColor.Black);

                            richText.AddText("Score: ")
                                .SetBold().SetFontColor(XLColor.DarkBlue);
                            richText.AddText($"{answerDto.Score}\n")
                                .SetFontColor(XLColor.Black);

                            richText.AddText("Comment: ")
                                .SetBold().SetFontColor(XLColor.DarkGreen);
                            richText.AddText($"{answerDto.Justification ?? "-"}")
                                .SetFontColor(XLColor.Black);

                            ws.Cell(row, c - 1).Style.Alignment.WrapText = true;
                            ws.Row(row).Height = 60;
                        }
                    }

                    row += 2;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }
    }
}