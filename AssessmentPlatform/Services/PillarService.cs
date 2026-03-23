using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
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

        public async Task<ResultResponseDto<List<PillarWithQuestionsDto>>> GetPillarsWithQuestions(GetCityPillarHistoryRequestDto request)
        {
            try
            {
                var year = request.UpdatedAt.Year;

                // 1. Validate user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserID == request.UserID);

                if (user == null)
                    return ResultResponseDto<List<PillarWithQuestionsDto>>.Failure(new[] { "Invalid user" });

                // 2. Role-based mapping filter
                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == request.CityID && x.AssignedByUserId == request.UserID || x.UserID == request.UserID,
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == request.CityID && x.UserID == request.UserID,
                    _ => x => !x.IsDeleted && x.CityID == request.CityID
                };

                var mappingIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                // 3. Load assessments
                var assessments = await _context.Assessments
                    .Include(a => a.UserCityMapping)
                    .Include(a => a.PillarAssessments)
                        .ThenInclude(pa => pa.Responses)
                    .Where(a => mappingIds.Contains(a.UserCityMappingID)
                                && a.IsActive
                                && a.UpdatedAt.Year == year
                                && (a.AssessmentPhase == AssessmentPhase.Completed
                                    || a.AssessmentPhase == AssessmentPhase.EditRejected
                                    || a.AssessmentPhase == AssessmentPhase.EditRequested))
                    .AsNoTracking()
                    .ToListAsync();

                // 4. Load pillars
                var pillars = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .Where(p => !request.PillarID.HasValue || p.PillarID == request.PillarID)
                    .OrderBy(p => p.DisplayOrder)
                    .AsNoTracking()
                    .ToListAsync();

                // 5. Users dictionary
                var userIds = assessments.Select(a => a.UserCityMapping.UserID).Distinct().ToList();

                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                // =========================================
                // ? Pre-group responses (Performance Boost)
                // =========================================
                var responseLookup = assessments
                    .SelectMany(a => a.PillarAssessments.Select(pa => new { a, pa }))
                    .SelectMany(x => x.pa.Responses.Select(r => new
                    {
                        Response = r,
                        x.pa.PillarID,
                        UserID = x.a.UserCityMapping.UserID
                    }))
                    .GroupBy(x => (x.Response.QuestionID, x.PillarID, x.UserID))
                    .ToDictionary(g => g.Key, g => g.First().Response);

                // =========================================
                // ? AI DATA FIXED
                // =========================================
                var aiRaw = await _context.AIEstimatedQuestionScores
                    .Where(x => x.CityID == request.CityID
                                && (!request.PillarID.HasValue || x.PillarID == request.PillarID)
                                && x.Year == year)
                    .ToListAsync();

                var aiDict = aiRaw
                    .GroupBy(x => new { x.PillarID, x.QuestionID })
                    .ToDictionary(
                        g => (g.Key.PillarID, g.Key.QuestionID),
                        g => g.Select(x => new QuestionUserAnswerDto
                        {
                            UserID = int.MaxValue,
                            FullName = "AI_Result",
                            QuestionID = x.QuestionID,
                            Score = (int?)x.AIScore,
                            Justification = x.EvidenceSummary,
                            OptionText = ""
                        }).FirstOrDefault()
                    );

                // =========================================
                // 6. Build response
                // =========================================
                var result = pillars.Select(p => new PillarWithQuestionsDto
                {
                    PillarID = p.PillarID,
                    PillarName = p.PillarName,
                    DisplayOrder = p.DisplayOrder,
                    TotalQuestions = p.Questions.Count(q => !q.IsDeleted),

                    Questions = p.Questions
                        .Where(q => !q.IsDeleted)
                        .OrderBy(q => q.DisplayOrder)
                        .Select(q =>
                        {
                            var userAnswers = new Dictionary<int, QuestionUserAnswerDto>();

                            foreach (var uid in userIds)
                            {
                                responseLookup.TryGetValue((q.QuestionID, p.PillarID, uid), out var response);

                                var option = q.QuestionOptions
                                    .FirstOrDefault(o => o.OptionID == response?.QuestionOptionID);

                                userAnswers[uid] = new QuestionUserAnswerDto
                                {
                                    UserID = uid,
                                    FullName = usersDict.TryGetValue(uid, out var name) ? name : "",
                                    QuestionID = q.QuestionID,
                                    Score = (int?)response?.Score,
                                    Justification = response?.Justification ?? "",
                                    OptionText = option?.OptionText ?? ""
                                };
                            }

                            // ? Inject AI answer
                            if (aiDict.TryGetValue((p.PillarID, q.QuestionID), out var aiAnswer))
                            {
                                if(aiAnswer !=null)
                                {
                                    var option = q.QuestionOptions
                                    .FirstOrDefault(o => o.ScoreValue == aiAnswer.Score);

                                    aiAnswer.OptionText = option?.OptionText ?? "";
                                    userAnswers[int.MaxValue] = aiAnswer;
                                }                                
                            }

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

                byte[] fileBytes;
                string fileName;

                if (requestDto.ExportType?.ToLower() == "pdf")
                {
                    // ? Use structured data directly (NO flattening)
                    fileBytes = GeneratePdf(response.Result, city, requestDto.UpdatedAt.Year);

                    fileName = $"ExportPillarsHistory_{requestDto.CityID}_{requestDto.PillarID}.pdf";
                }
                else
                {
                    // ? Excel (existing)
                    fileBytes = MakePillarSheet(response.Result, city);
                    fileName = $"ExportPillarsHistory_{requestDto.CityID}_{requestDto.PillarID}.xlsx";
                }

                return new("ExportPillarsHistory"+ requestDto.CityID+""+requestDto.PillarID, fileBytes);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportPillarsHistoryByUserId", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
        public byte[] GeneratePdf(List<PillarWithQuestionsDto> data, City city, int year)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/veridian-urban-index.png");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);

                    page.Content().Column(col =>
                    {
                        int pillarIndex = 1;

                        foreach (var pillar in data)
                        {
                            // ================= HEADER =================
                            col.Item()
                                .Background("#1f4b3f")
                                .Padding(15)
                                .Row(row =>
                                {
                                    row.RelativeItem().Column(left =>
                                    {
                                        left.Item().Text($"{pillarIndex}. {pillar.PillarName}")
                                            .FontSize(18)
                                            .Bold()
                                            .FontColor("#ffffff");

                                        left.Item().Text($"{city?.CityName}, {city?.State}, USA | Data Year: {year}")
                                            .FontSize(10)
                                            .FontColor("#cfe7df");

                                        left.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                                            .FontSize(9)
                                            .FontColor("#cfe7df");
                                    });

                                    // Right logo
                                    row.ConstantItem(80)
                                       .Background("#ffffff")
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Padding(4)
                                        .Image(logoPath)
                                        .FitArea();

                                });

                            col.Item().PaddingBottom(10);

                            int questionIndex = 1;

                            foreach (var question in pillar.Questions)
                            {
                                string questionNumber = $"{pillarIndex}.{questionIndex}";

                                // ================= QUESTION CARD =================
                                col.Item()
                                    .Background("#ffffff")
                                    .Border(1)
                                    .BorderColor("#e5e5e5")
                                    .Padding(12)
                                    .Column(qCol =>
                                    {
                                        // Question Title
                                        qCol.Item().Text($"{questionNumber} {question.QuestionText}")
                                            .FontSize(12)
                                            .Bold();

                                        qCol.Item().PaddingTop(10);

                                        // ================= CLEAN TABLE =================
                                        qCol.Item().Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn(2); // Name
                                                columns.RelativeColumn(1); // Score
                                                columns.RelativeColumn(5); // Option
                                            });

                                            // HEADER
                                            table.Header(header =>
                                            {
                                                header.Cell().PaddingBottom(5)
                                                    .Text("Name").SemiBold().FontSize(10);

                                                header.Cell().PaddingBottom(5)
                                                    .Text("Score").SemiBold().FontSize(10);

                                                header.Cell().PaddingBottom(5)
                                                    .Text("Option").SemiBold().FontSize(10);
                                            });

                                            // ROWS
                                            foreach (var user in question.Users.Values
                                                         .OrderBy(x => x.UserID == -1 ? 1 : 0))
                                            {
                                                bool isAI = user.UserID == -1;
                                                string bgColor = isAI ? "#e6f4ef" : "#ffffff";

                                                // NAME
                                                var nameCell = table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(isAI ? "AI" : user.FullName)
                                                    .FontColor(isAI ? "#0a7d5e" : "#000");

                                                if (isAI)
                                                    nameCell.Bold();

                                                // SCORE
                                                table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(user.Score?.ToString() ?? "");

                                                // OPTION
                                                table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(user.OptionText ?? "");
                                            }
                                        });
                                    });

                                questionIndex++;
                                col.Item().PaddingBottom(10);
                            }

                            pillarIndex++;
                            col.Item().PaddingBottom(15);
                        }
                    });
                });
            }).GeneratePdf();
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
        public async Task<PaginationResponse<PillarsHistroyResponseDto>> GetResponsesByUserId(GetPillarResponseHistoryRequestNewDto request, UserRole userRole)
        {
            try
            {
                var year = request.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                // Role based filter
                IQueryable<UserCityMapping> userCityMappings = _context.UserCityMappings
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CityID == request.CityID);

                userCityMappings = userRole switch
                {
                    UserRole.Analyst => userCityMappings.Where(x => x.AssignedByUserId == request.UserId || x.UserID == request.UserId),
                    UserRole.Evaluator => userCityMappings.Where(x => x.UserID == request.UserId),
                    _ => userCityMappings
                };

                // =========================
                // 1. USER DATA
                // =========================
                var rawData = await (
                    from ucm in userCityMappings
                    join a in _context.Assessments on ucm.UserCityMappingID equals a.UserCityMappingID
                    where a.IsActive &&
                          (a.UpdatedAt >= startDate && a.UpdatedAt <= endDate &&
                           (a.AssessmentPhase == AssessmentPhase.Completed
                            || a.AssessmentPhase == AssessmentPhase.EditRejected
                            || a.AssessmentPhase == AssessmentPhase.EditRequested))
                    from pa in a.PillarAssessments
                    where !request.PillarID.HasValue || pa.PillarID == request.PillarID
                    select new
                    {
                        pa.PillarID,
                        UserID = ucm.UserID,
                        Responses = pa.Responses
                    }
                ).ToListAsync();

                // Users dictionary
                var userIds = rawData.Select(x => x.UserID).Distinct().ToList();

                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                // =========================
                // 2. AI DATA
                // =========================
                var aiDataList = await _context.AIEstimatedQuestionScores
                    .Where(x => x.CityID == request.CityID
                        && (!request.PillarID.HasValue || x.PillarID == request.PillarID)
                        && x.Year == year)
                    .GroupBy(x => x.PillarID)
                    .Select(g => new
                    {
                        PillarID = g.Key,
                        Score = g.Sum(x => x.AIScore ?? 0),
                        ScoreProgress = g.Average(x => x.AIProgress ?? 0),
                        Count = g.Count()
                    })
                    .ToListAsync();

                var aiData = aiDataList.ToDictionary(
                    x => x.PillarID,
                    x => new PillarsUserHistroyResponseDto
                    {
                        UserID = int.MaxValue,
                        FullName = "AI_Result",
                        Score = Math.Round(x.Score, 0),
                        ScoreProgress = x.ScoreProgress,
                        TotalQuestion = x.Count,
                        AnsQuestion = x.Count,
                        AnsPillar = 1
                    }
                );

                // =========================
                // 3. ALL PILLARS (MAIN FIX)
                // =========================
                var pillars = await _context.Pillars
                    .Where(p => !request.PillarID.HasValue || p.PillarID == request.PillarID)
                    .Select(p => new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.DisplayOrder,
                        TotalQuestion = p.Questions.Count()
                    })
                    .ToListAsync();

                // =========================
                // 4. FINAL RESULT (FROM PILLARS)
                // =========================
                var result = pillars
                    .Select(p =>
                    {
                        var pillarRawData = rawData
                            .Where(x => x.PillarID == p.PillarID)
                            .ToList();

                        var users = pillarRawData
                            .GroupBy(x => x.UserID)
                            .Select(userGroup =>
                            {
                                var responses = userGroup
                                    .SelectMany(x => x.Responses)
                                    .Where(r => r.Score.HasValue &&
                                                (int)r.Score.Value <= (int)ScoreValue.Four)
                                    .ToList();

                                var score = responses.Sum(r => (int?)r.Score ?? 0);
                                var scoreCount = responses.Count;

                                decimal progress = scoreCount > 0
                                    ? score * 100m / (scoreCount * 4m)
                                    : 0m;

                                return new PillarsUserHistroyResponseDto
                                {
                                    UserID = userGroup.Key,
                                    FullName = usersDict.GetValueOrDefault(userGroup.Key, ""),
                                    Score = score,
                                    ScoreProgress = progress,
                                    TotalQuestion = p.TotalQuestion,
                                    AnsQuestion = responses.Count,
                                    AnsPillar = responses.Any() ? 1 : 0
                                };
                            })
                            .ToList();

                        // ? Insert AI row (always)
                        if (aiData.TryGetValue(p.PillarID, out var aiPillar))
                        {
                            users.Insert(0, aiPillar);
                        }
                        else
                        {
                            users.Insert(0, new PillarsUserHistroyResponseDto
                            {
                                UserID = int.MaxValue,
                                FullName = "AI_Result",
                                Score = 0,
                                ScoreProgress = 0,
                                TotalQuestion = p.TotalQuestion,
                                AnsQuestion = 0,
                                AnsPillar = 0
                            });
                        }

                        return new PillarsHistroyResponseDto
                        {
                            PillarID = p.PillarID,
                            PillarName = p.PillarName,
                            DisplayOrder = p.DisplayOrder,
                            Users = users
                        };
                    })
                    .OrderBy(x => x.DisplayOrder)
                    .ToList();

                // =========================
                // 5. PAGINATION
                // =========================
                var count = 0;
                var valid = 0;
                var totalRecords = 0;

                foreach (var r in result)
                {
                    totalRecords += r.Users.Count;
                    if (count + r.Users.Count <= request.PageSize)
                    {
                        count += r.Users.Count;
                        valid++;
                    }
                }

                var filterResult = result.Skip((request.PageNumber - 1) * valid);

                return new PaginationResponse<PillarsHistroyResponseDto>
                {
                    Data = filterResult.Take(valid),
                    TotalRecords = totalRecords,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetPillarsHistoryByUserId", ex);

                return new PaginationResponse<PillarsHistroyResponseDto>();
            }
        }
    }
}