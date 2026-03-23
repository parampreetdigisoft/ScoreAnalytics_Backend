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
using System.Text;
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
        public async Task<ResultResponseDto<GetPillarQuestionByCityRespones>> GetQuestionsByCityIdAsync(CityPillerRequestDto request, int userId)
        {
            try
            {
                var valid = _context.UserCityMappings.Any(x => x.UserCityMappingID == request.UserCityMappingID && x.UserID == userId && !x.IsDeleted);
                if (valid)
                {
                    var year = DateTime.Now.Year;
                    // Load assessment once (if exists)
                    var answeredPillarIds = new List<int>();
                    var assessment = await _context.Assessments
                        .Include(x => x.PillarAssessments).ThenInclude(x => x.Responses)
                        .Where(a => a.UserCityMappingID == request.UserCityMappingID && a.UpdatedAt.Year == year && a.IsActive)
                        .FirstOrDefaultAsync();
                    if (assessment != null)
                    {
                        answeredPillarIds = assessment.PillarAssessments
                       .Select(r => r.PillarID)
                       .ToList();
                    }
                    if (assessment != null && answeredPillarIds.Count == 14 && !request.PillarID.HasValue)
                    {
                        request.PillarID = assessment.PillarAssessments.First().PillarID;
                    }

                    // Get next unanswered pillar
                    var selectPillar = await _context.Pillars
                        .Include(p => p.Questions)
                            .ThenInclude(q => q.QuestionOptions)
                        .Where(p => !request.PillarID.HasValue ? !answeredPillarIds.Contains(p.PillarID) : p.PillarID == request.PillarID)
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
                    if (assessment != null)
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
                                Justification = submittedQuestion.Justification,
                                Source = submittedQuestion.Source
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
                return null;

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
                                join u in _context.Users on m.UserID equals u.UserID 
                                where m.UserCityMappingID == userCityMappingID
                                select new
                                {
                                    CityName = c.CityName,
                                    FullName = u.FullName
                                }).FirstOrDefault();

                var sheetName = fileName?.CityName + "_" + fileName?.FullName;

                // Get next unanswered pillar
                var nextPillars = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
                var year = DateTime.Now.Year;
                var pillarAssessments = _context.Assessments
                    .Include(x=>x.PillarAssessments)
                    .ThenInclude(x=>x.Responses)
                    .Where(a => a.UserCityMappingID == userCityMappingID && a.IsActive && a.UpdatedAt.Year == year)
                    .SelectMany(x => x.PillarAssessments).ToList();

                var byteArray = MakePillarSheetClientReadable_Updated(nextPillars, pillarAssessments, userCityMappingID, fileName);

                return new(sheetName, byteArray);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportAssessment", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }




        // ============================================================
        //  EXPORT — MakePillarSheetClientReadable_Updated
        //  IMPORT — ImportAssessmentAsync
        //
        //  Key changes vs old version:
        //  • __OptionData hidden sheet drives per-question dropdowns
        //  • 4 rows per question: Answer | Comment | Source | Separator
        //  • Col J (hidden) holds numeric score formula → clean SUM / AVERAGE
        //  • Better visual design: freeze pane, colour palette, borders
        // ============================================================

        // ── Row layout constants ─────────────────────────────────────
        // Row 1  : Title bar
        // Row 2  : Pillar name
        // Row 3  : City / Year
        // Row 4  : Evaluator
        // Row 5  : (thin gap)
        // Row 6  : Pillar description
        // Row 7  : (thin gap)
        // Row 8  : Column headers  ← freeze above here
        // Row 9+ : Questions (4 rows each)
        //            row+0  Answer   (col A=sno, B=question, C="Answer",  D=dropdown ← EDITABLE)
        //            row+1  Comment  (col B=hint, C="Comment", D=text     ← EDITABLE)
        //            row+2  Source   (col C="Source", D=text              ← EDITABLE)
        //                            col K-O = hidden IDs
        //            row+3  Separator (thin coloured rule)

        private const int FIRST_Q_ROW = 9;
        private const int ROWS_PER_Q = 4;

        // ── Colour palette ───────────────────────────────────────────
        private static readonly XLColor ColHeaderBlue = XLColor.FromArgb(31, 73, 125);
        private static readonly XLColor ColAccentBlue = XLColor.FromArgb(68, 114, 196);
        private static readonly XLColor ColLightBlue = XLColor.FromArgb(219, 229, 241);
        private static readonly XLColor ColRowAlt = XLColor.FromArgb(245, 248, 255);
        private static readonly XLColor ColEditableYellow = XLColor.FromArgb(255, 253, 215);
        private static readonly XLColor ColSeparator = XLColor.FromArgb(210, 222, 240);
        private static readonly XLColor ColGrayText = XLColor.FromArgb(130, 130, 130);
        private static readonly XLColor ColDescBg = XLColor.FromArgb(255, 251, 224);
        private static readonly XLColor ColDescBorder = XLColor.FromArgb(200, 175, 70);
        private static readonly XLColor ColTotalBg = XLColor.FromArgb(228, 239, 255);
        private static readonly XLColor ColInputBorder = XLColor.FromArgb(180, 200, 230);

        // ────────────────────────────────────────────────────────────
        //  EXPORT
        // ────────────────────────────────────────────────────────────
        private byte[] MakePillarSheetClientReadable_Updated(
            List<Pillar> pillars,
            List<PillarAssessment> pillarAssessments,
            int userCityMappingID,
            dynamic? cityUser)
        {
            using var workbook = new XLWorkbook();

            // Hidden sheet that stores all option texts for dropdown validation.
            // Excel data-validation lists must point to a worksheet range, so we
            // write each question's options into a contiguous block here.
            var optWs = workbook.Worksheets.Add("__OptionData");
            optWs.Visibility = XLWorksheetVisibility.VeryHidden;
            int optRow = 1; // global pointer into __OptionData col A

            foreach (var pillar in pillars)
            {
                var ws = workbook.Worksheets.Add(GetValidSheetName(pillar.PillarName));

                // ── Column widths ─────────────────────────────────────
                ws.Column(1).Width = 6;   // #
                ws.Column(2).Width = 72;  // Question
                ws.Column(3).Width = 13;  // Field label
                ws.Column(4).Width = 52;  // Response (dropdown / text)

                // ── Row 1 : Title ─────────────────────────────────────
                var title = ws.Range("A1:D1").Merge();
                title.Value = "VERIDIAN URBAN INDEX — CITY ASSESSMENT";
                title.Style.Font.Bold = true;
                title.Style.Font.FontSize = 13;
                title.Style.Font.FontColor = XLColor.White;
                title.Style.Fill.BackgroundColor = ColHeaderBlue;
                title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(1).Height = 26;

                // ── Row 2 : Pillar name ───────────────────────────────
                var pillarTitle = ws.Range("A2:D2").Merge();
                pillarTitle.Value = $"Pillar: {pillar.PillarName}";
                pillarTitle.Style.Font.Bold = true;
                pillarTitle.Style.Font.FontSize = 11;
                pillarTitle.Style.Font.FontColor = XLColor.White;
                pillarTitle.Style.Fill.BackgroundColor = ColAccentBlue;
                pillarTitle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(2).Height = 20;

                // ── Rows 3-4 : Meta ───────────────────────────────────
                ws.Cell(3, 1).Value = "City:";
                ws.Cell(3, 2).Value = cityUser?.CityName?.ToString() ?? "";
                ws.Cell(3, 3).Value = "Year:";
                ws.Cell(3, 4).Value = DateTime.Now.Year;
                ws.Cell(4, 1).Value = "Evaluator:";
                ws.Cell(4, 2).Value = cityUser?.FullName?.ToString() ?? "";

                foreach (int r in new[] { 3, 4 })
                {
                    var mr = ws.Range(r, 1, r, 4);
                    mr.Style.Fill.BackgroundColor = ColLightBlue;
                    mr.Style.Font.FontSize = 10;
                    ws.Cell(r, 1).Style.Font.Bold = true;
                    ws.Cell(r, 3).Style.Font.Bold = true;
                    ws.Row(r).Height = 18;
                }

                // ── Row 5 : thin gap ──────────────────────────────────
                ws.Row(5).Height = 4;

                // ── Row 6 : Pillar description ─────────────────────────
                var desc = ws.Range("A6:D6").Merge();
                desc.Value = CleanHtml(pillar.Description);
                desc.Style.Fill.BackgroundColor = ColDescBg;
                desc.Style.Font.FontSize = 10;
                desc.Style.Alignment.WrapText = true;
                desc.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                desc.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                desc.Style.Border.OutsideBorderColor = ColDescBorder;
                ws.Row(6).Height = 120;

                // ── Row 7 : thin gap ──────────────────────────────────
                ws.Row(7).Height = 4;

                // ── Row 8 : Column headers ────────────────────────────
                ws.Cell(8, 1).Value = "#";
                ws.Cell(8, 2).Value = "Question";
                ws.Cell(8, 3).Value = "Field";
                ws.Cell(8, 4).Value = "Response  ▼  (select from dropdown)";

                var hdr = ws.Range(8, 1, 8, 4);
                hdr.Style.Font.Bold = true;
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Fill.BackgroundColor = ColHeaderBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                hdr.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Row(8).Height = 20;

                // Freeze rows 1-8 so header stays on screen while scrolling
                ws.SheetView.FreezeRows(8);

                // ── Questions ─────────────────────────────────────────
                int row = FIRST_Q_ROW;
                int sno = 1;
                var scoreRowNums = new List<int>(); // rows that carry a score (Col J formula)

                var submittedResponses = pillarAssessments
                    .Where(x => x.PillarID == pillar.PillarID)
                    .SelectMany(x => x.Responses)
                    .ToDictionary(x => x.QuestionID);

                foreach (var q in pillar.Questions.OrderBy(x => x.DisplayOrder))
                {
                    submittedResponses.TryGetValue(q.QuestionID, out var ans);
                    ans ??= new();

                    bool isEven = (sno % 2 == 0);
                    XLColor qBg = isEven ? ColRowAlt : XLColor.White;

                    // ── Build option texts (score desc first, then N/A / Unknown) ──
                    var options = (q.QuestionOptions ?? new List<QuestionOption>())
                                  .OrderByDescending(x => x.ScoreValue)
                                  .ThenBy(x => x.OptionText)
                                  .ToList();

                    var optionTexts = options.Select(opt =>
                    {
                        string prefix = opt.ScoreValue.HasValue ? $"{opt.ScoreValue} - " : "";
                        return (prefix + opt.OptionText.Trim()).Trim();
                    }).ToList();

                    // Write option texts to hidden sheet and record range
                    int qOptStart = optRow;
                    foreach (var txt in optionTexts)
                        optWs.Cell(optRow++, 1).Value = txt;
                    int qOptEnd = optRow - 1;   // inclusive end row in __OptionData

                    // ── Create a workbook-level Named Range for this question's options.
                    //    ClosedXML does NOT support cross-sheet sheet-references directly
                    //    in dv.Value ("'Sheet'!$A$1:$A$7" silently fails).
                    //    A Named Range IS recognised by Excel and works correctly.
                    string namedRangeKey = $"Opts_Q{q.QuestionID}";
                    // Remove any stale definition (e.g. re-export after code change)
                    if (workbook.NamedRanges.Contains(namedRangeKey))
                        workbook.NamedRanges.Delete(namedRangeKey);
                    workbook.NamedRanges.Add(namedRangeKey,
                        optWs.Range(qOptStart, 1, qOptEnd, 1));

                    // ── Current answer text (pre-fill if data exists) ──
                    string currentAnswer = "";
                    if (ans.QuestionOptionID > 0)
                    {
                        var sel = options.FirstOrDefault(x => x.OptionID == ans.QuestionOptionID);
                        if (sel != null)
                        {
                            string prefix = sel.ScoreValue.HasValue ? $"{sel.ScoreValue} - " : "";
                            currentAnswer = (prefix + sel.OptionText.Trim()).Trim();
                        }
                    }

                    int ansRow = row;
                    scoreRowNums.Add(ansRow);

                    // ── Col A : S.No ──────────────────────────────────
                    ws.Cell(ansRow, 1).Value = sno++;
                    ws.Cell(ansRow, 1).Style.Font.Bold = true;
                    ws.Cell(ansRow, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(ansRow, 1).Style.Fill.BackgroundColor = ColAccentBlue;
                    ws.Cell(ansRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(ansRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // ── Col B : Question text ─────────────────────────
                    ws.Cell(ansRow, 2).Value = q.QuestionText;
                    ws.Cell(ansRow, 2).Style.Font.Bold = true;
                    ws.Cell(ansRow, 2).Style.Font.FontSize = 10;
                    ws.Cell(ansRow, 2).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(ansRow, 2).Style.Alignment.WrapText = true;
                    ws.Cell(ansRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // ── Col C : Label ─────────────────────────────────
                    ws.Cell(ansRow, 3).Value = "Answer";
                    ws.Cell(ansRow, 3).Style.Font.Bold = true;
                    ws.Cell(ansRow, 3).Style.Font.FontColor = ColAccentBlue;
                    ws.Cell(ansRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(ansRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(ansRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // ── Col D : Dropdown answer cell ──────────────────
                    var ansCell = ws.Cell(ansRow, 4);
                    ansCell.Value = currentAnswer;
                    ansCell.Style.Fill.BackgroundColor = ColEditableYellow;
                    ansCell.Style.Alignment.WrapText = true;
                    ansCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ansCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ansCell.Style.Border.OutsideBorderColor = ColAccentBlue;

                    // DATA VALIDATION — list via Named Range (cross-sheet refs don't work in ClosedXML dv.Value)
                    if (optionTexts.Any())
                    {
                        var dv = ansCell.GetDataValidation();
                        dv.Clear();
                        dv.AllowedValues = XLAllowedValues.List;
                        // Reference the Named Range we created above — this IS supported by ClosedXML
                        // and produces a real clickable dropdown arrow in Excel / LibreOffice.
                        dv.Value = namedRangeKey;
                        dv.IgnoreBlanks = true;
                        dv.ShowInputMessage = true;
                        dv.InputTitle = "Select Answer";
                        dv.InputMessage = "Click the dropdown arrow ▼ to choose a score option.";
                        dv.ShowErrorMessage = true;
                        dv.ErrorTitle = "Invalid Entry";
                        dv.ErrorMessage = "Please select a value from the dropdown list provided.";
                        dv.ErrorStyle = XLErrorStyle.Warning; // warn but allow manual override
                    }

                    ws.Row(ansRow).Height = 45;

                    // ── Col J (hidden) : Numeric score formula ─────────
                    // Extracts the leading digit from the dropdown text (e.g. "3 - Good...") → 3
                    // Returns "" for N/A, Unknown, or blank
                    ws.Cell(ansRow, 10).FormulaA1 =
                        $"=IFERROR(" +
                        $"IF(LEN(D{ansRow})>0," +
                        $"  IF(ISNUMBER(VALUE(LEFT(D{ansRow},1))), VALUE(LEFT(D{ansRow},1)), \"\")," +
                        $"  \"\")," +
                        $"\"\")";

                    // ── Comment row (row+1) ───────────────────────────
                    int commentRow = ansRow + 1;

                    ws.Cell(commentRow, 1).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 2).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 2).Value = "Justification / supporting evidence:";
                    ws.Cell(commentRow, 2).Style.Font.Italic = true;
                    ws.Cell(commentRow, 2).Style.Font.FontColor = ColGrayText;
                    ws.Cell(commentRow, 2).Style.Font.FontSize = 9;
                    ws.Cell(commentRow, 2).Style.Alignment.WrapText = true;
                    ws.Cell(commentRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(commentRow, 3).Value = "Comment";
                    ws.Cell(commentRow, 3).Style.Font.Bold = true;
                    ws.Cell(commentRow, 3).Style.Font.FontColor = ColGrayText;
                    ws.Cell(commentRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(commentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(commentRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(commentRow, 4).Value = ans.Justification ?? "";
                    ws.Cell(commentRow, 4).Style.Alignment.WrapText = true;
                    ws.Cell(commentRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ws.Cell(commentRow, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(252, 252, 252);
                    ws.Cell(commentRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(commentRow, 4).Style.Border.OutsideBorderColor = ColInputBorder;
                    ws.Row(commentRow).Height = 40;

                    // ── Source row (row+2) — also carries hidden IDs ──
                    int sourceRow = ansRow + 2;

                    ws.Cell(sourceRow, 1).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(sourceRow, 2).Style.Fill.BackgroundColor = qBg;

                    ws.Cell(sourceRow, 3).Value = "Source";
                    ws.Cell(sourceRow, 3).Style.Font.Bold = true;
                    ws.Cell(sourceRow, 3).Style.Font.FontColor = ColGrayText;
                    ws.Cell(sourceRow, 3).Style.Fill.BackgroundColor = qBg;
                    ws.Cell(sourceRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(sourceRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    ws.Cell(sourceRow, 4).Value = ans.Source ?? "";
                    ws.Cell(sourceRow, 4).Style.Alignment.WrapText = true;
                    ws.Cell(sourceRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ws.Cell(sourceRow, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(252, 252, 252);
                    ws.Cell(sourceRow, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(sourceRow, 4).Style.Border.OutsideBorderColor = ColInputBorder;
                    ws.Row(sourceRow).Height = 25;

                    // Hidden IDs (cols K–O = 11–15)
                    ws.Cell(sourceRow, 11).Value = userCityMappingID;
                    ws.Cell(sourceRow, 12).Value = pillar.PillarID;
                    ws.Cell(sourceRow, 13).Value = q.QuestionID;
                    ws.Cell(sourceRow, 14).Value = ans.QuestionOptionID;
                    ws.Cell(sourceRow, 15).Value = ans.ResponseID;

                    // ── Separator row (row+3) ─────────────────────────
                    int sepRow = ansRow + 3;
                    ws.Range(sepRow, 1, sepRow, 4).Style.Fill.BackgroundColor = ColSeparator;
                    ws.Row(sepRow).Height = 3;

                    row += ROWS_PER_Q;
                }

                // ── Totals section ────────────────────────────────────
                row++; // one blank row gap

                // Helper to build comma-separated J-column references
                string JRefs() => string.Join(",", scoreRowNums.Select(r => $"J{r}"));

                // Total Score row
                ws.Cell(row, 3).Value = "Total Score";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontSize = 11;
                ws.Cell(row, 3).Style.Font.FontColor = ColHeaderBlue;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                string totalF = scoreRowNums.Any() ? $"=SUM({JRefs()})" : "=0";
                ws.Cell(row, 4).FormulaA1 = totalF;
                ws.Cell(row, 4).Style.Font.Bold = true;
                ws.Cell(row, 4).Style.Font.FontSize = 13;
                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Cell(row, 4).Style.Border.OutsideBorderColor = ColAccentBlue;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 24;
                row++;

                // Average Score row
                ws.Cell(row, 3).Value = "Average Score";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontColor = ColHeaderBlue;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // SUM/COUNT approach works reliably with non-contiguous cells
                string avgF = scoreRowNums.Any()
                    ? $"=IFERROR(SUM({JRefs()})/COUNT({JRefs()}),\"\")"
                    : "=\"\"";
                ws.Cell(row, 4).FormulaA1 = avgF;
                ws.Cell(row, 4).Style.Font.Bold = true;
                ws.Cell(row, 4).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Cell(row, 4).Style.Border.OutsideBorderColor = ColAccentBlue;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 22;
                row++;

                // Answered / Total count row
                ws.Cell(row, 3).Value = "Answered";
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.FontColor = ColGrayText;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // DRefs: comma-joined D-column answer cells

                string answeredF = scoreRowNums.Any()
                    ? $"=COUNT({JRefs()}) & \" / {scoreRowNums.Count}\""
                    : "=\"0 / 0\"";

                ws.Cell(row, 4).FormulaA1 = answeredF;

                ws.Cell(row, 4).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = ColTotalBg;
                ws.Row(row).Height = 20;

                // ── Hide auxiliary columns ────────────────────────────
                ws.Column(10).Hide(); // Score formula (J)
                ws.Column(11).Hide(); // userCityMappingID
                ws.Column(12).Hide(); // pillarID
                ws.Column(13).Hide(); // questionID
                ws.Column(14).Hide(); // questionOptionID
                ws.Column(15).Hide(); // responseID
            }

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            html = html.Replace("&nbsp;", " ")
                       .Replace("<br>", "\n")
                       .Replace("<br/>", "\n")
                       .Replace("<p>", "")
                       .Replace("</p>", "\n");

            // remove any remaining tags
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();
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
                var year = requestDto.UpdatedAt.Year;

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserID == requestDto.UserID);

                if (!requestDto.PillarID.HasValue || user == null)
                {
                    return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                        .Failure(new[] { "Invalid request" });
                }

                // =========================
                // 1. PILLAR + QUESTIONS
                // =========================
                var pillar = await _context.Pillars
                    .Include(x => x.Questions)
                        .ThenInclude(x => x.QuestionOptions)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PillarID == requestDto.PillarID.Value);

                if (pillar == null)
                {
                    return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                        .Failure(new[] { "Pillar not found" });
                }

                // =========================
                // 2. USER MAPPINGS
                // =========================
                var userMappings = await _context.UserCityMappings
                    .Where(x => x.CityID == requestDto.CityID
                                && !x.IsDeleted
                                && (x.AssignedByUserId == requestDto.UserID
                                    || x.UserID == requestDto.UserID
                                    || user.Role == UserRole.Admin))
                    .AsNoTracking()
                    .ToListAsync();

                var mappingIds = userMappings.Select(x => x.UserCityMappingID).ToList();

                // =========================
                // 3. ASSESSMENTS
                // =========================
                var assessments = await _context.Assessments
                    .Include(x => x.UserCityMapping)
                    .Include(a => a.PillarAssessments
                        .Where(pa => pa.PillarID == requestDto.PillarID))
                    .ThenInclude(pa => pa.Responses)
                    .Where(a => mappingIds.Contains(a.UserCityMappingID)
                                && a.IsActive
                                && a.UpdatedAt.Year == year)
                    .AsNoTracking()
                    .ToListAsync();

                var userIds = assessments.Select(x => x.UserCityMapping.UserID).Distinct().ToList();

                // =========================
                // 4. USERS DICTIONARY
                // =========================
                var users = await _context.Users
                    .Where(x => userIds.Contains(x.UserID))
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.UserID);

                // =========================
                // 5. USER RESPONSES
                // =========================
                var responsesByUser = assessments
                    .GroupBy(a => a.UserCityMapping.UserID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(a => a.PillarAssessments)
                              .SelectMany(pa => pa.Responses)
                              .ToDictionary(r => r.QuestionID)
                    );

                // =========================
                // 6. AI DATA (NEW)
                // =========================
                var aiRaw = await _context.AIEstimatedQuestionScores
                    .Where(x => x.CityID == requestDto.CityID
                                && x.PillarID == requestDto.PillarID
                                && x.Year == year)
                    .ToListAsync();

                var aiDict = aiRaw
                    .GroupBy(x => x.QuestionID)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            Score = Math.Round(g.Average(x => x.AIScore ?? 0), 0),
                            Progress = g.Average(x => x.AIProgress ?? 0),
                            EvidenceSummary = g.FirstOrDefault()?.EvidenceSummary ?? ""
                        }
                    );

                // =========================
                // 7. FINAL RESULT
                // =========================
                var pillarResponses = pillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        var userInfos = userIds.Select(uid =>
                        {
                            users.TryGetValue(uid, out var u);
                            responsesByUser.TryGetValue(uid, out var userResponseDict);

                            (userResponseDict ?? new()).TryGetValue(q.QuestionID, out var response);

                            var optionID = response?.QuestionOptionID;
                            var option = q.QuestionOptions.FirstOrDefault(x => x.OptionID == optionID);

                            return new QuestionsByUserInfo
                            {
                                UserID = uid,
                                FullName = u?.FullName ?? string.Empty,
                                Score = response != null ? (int?)response.Score : null,
                                OptionText = option?.OptionText ?? "",
                                Justification = response?.Justification ?? string.Empty
                            };
                        }).ToList();

                        // ? ADD AI RESULT ROW
                        if (aiDict.TryGetValue(q.QuestionID, out var ai))
                        {
                            var option = q.QuestionOptions.FirstOrDefault(x => x.ScoreValue == ai.Score);


                            userInfos.Insert(0, new QuestionsByUserInfo
                            {
                                UserID = int.MaxValue,
                                FullName = "AI_Result",
                                Score = (int?)ai.Score,
                                OptionText = option?.OptionText ?? "OptionText",
                                Justification = ai.EvidenceSummary
                            });
                        }
                        else
                        {
                            // default AI row
                            userInfos.Insert(0, new QuestionsByUserInfo
                            {
                                UserID = int.MaxValue,
                                FullName = "AI_Result",
                                Score = null,
                                OptionText = "",
                                Justification = ""
                            });
                        }

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

                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                    .Success(pillarResponses, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetQuestionsHistoryByPillar", ex);

                return ResultResponseDto<List<QuestionsByUserPillarsResponsetDto>>
                    .Failure(new[] { "There was an error, please try again later" });
            }
        }


        public async Task<ResultResponseDto<GetPillarQuestionByCityRespones>> GetQuestionsByCityMappingIdForAnalyst(
            CityPillerRequestDto request, int userId)
        {
            try
            {
                var userCityMappings = await _context.UserCityMappings
                    .FirstOrDefaultAsync(x => x.UserCityMappingID == request.UserCityMappingID
                                           && x.UserID == userId
                                           && !x.IsDeleted);

                if (userCityMappings == null)
                    return null;

                var year = DateTime.Now.Year;

                // Load assessment with related data
                var assessment = await _context.Assessments
                    .Include(x => x.PillarAssessments)
                        .ThenInclude(x => x.Responses)
                    .Where(a => a.UserCityMappingID == request.UserCityMappingID
                             && a.UpdatedAt.Year == year
                             && a.IsActive)
                    .FirstOrDefaultAsync();

                var answeredPillarIds = assessment?.PillarAssessments
                    .Select(r => r.PillarID)
                    .ToList() ?? new List<int>();

                if (assessment != null && answeredPillarIds.Count == 14 && !request.PillarID.HasValue)
                    request.PillarID = assessment.PillarAssessments.First().PillarID;

                // Get the target pillar (next unanswered or specific)
                var selectPillar = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .Where(p => !request.PillarID.HasValue
                        ? !answeredPillarIds.Contains(p.PillarID)
                        : p.PillarID == request.PillarID)
                    .OrderBy(p => p.DisplayOrder)
                    .FirstOrDefaultAsync();

                var nextUnansweredPillar = await _context.Pillars
                    .Where(p => !answeredPillarIds.Contains(p.PillarID))
                    .OrderBy(p => p.DisplayOrder)
                    .FirstOrDefaultAsync();

                if (selectPillar?.Questions == null)
                    return ResultResponseDto<GetPillarQuestionByCityRespones>.Failure(
                        new[] { "You have submitted assessment for this city" });

                // Build lookup for existing responses for the selected pillar
                var editAssessmentResponse = assessment?.PillarAssessments
                    .Where(a => a.PillarID == request.PillarID)
                    .SelectMany(x => x.Responses)
                    .ToDictionary(x => x.QuestionID)
                    ?? new Dictionary<int, AssessmentResponse>();

                // Build option text lookup for history display
                var optionTextLookup = selectPillar.Questions
                    .SelectMany(q => q.QuestionOptions)
                    .ToDictionary(o => o.OptionID, o => o.OptionText);

                // Project questions with pre-filled answers
                var questions = selectPillar.Questions
                    .OrderBy(q => q.DisplayOrder)
                    .Select(q =>
                    {
                        editAssessmentResponse.TryGetValue(q.QuestionID, out var submitted);
                        submitted ??= new AssessmentResponse();

                        return new AssessmentQuestionResponseDto
                        {
                            QuestionID = q.QuestionID,
                            QuestionText = q.QuestionText,
                            PillarID = q.PillarID,
                            ResponseID = submitted.ResponseID,
                            IsSelected = submitted.QuestionID == q.QuestionID,
                            History = new List<HistoryQuestionAnswerRawDto>(),
                            QuestionOptions = q.QuestionOptions.Select(x => new QuestionOptionDto
                            {
                                DisplayOrder = x.DisplayOrder,
                                OptionID = x.OptionID,
                                QuestionID = x.QuestionID,
                                IsSelected = submitted.QuestionOptionID == x.OptionID,
                                OptionText = x.OptionText,
                                ScoreValue = x.ScoreValue,
                                Justification = submitted.QuestionOptionID == x.OptionID ? submitted.Justification : string.Empty,
                                Source = submitted.QuestionOptionID == x.OptionID ? submitted.Source : string.Empty
                            }).ToList()
                        };
                    }).ToList();

                // Load city-level user mappings assigned by this analyst
                var userCityMappingsList = await _context.UserCityMappings
                    .Where(x => x.CityID == userCityMappings.CityID
                             && x.AssignedByUserId == userCityMappings.UserID)
                    .AsNoTracking()
                    .ToListAsync();

                var mappingIds = userCityMappingsList.Select(x => x.UserCityMappingID).ToList();
                var userIds = userCityMappingsList.Select(x => x.UserID).ToList();

                var users = await _context.Users
                    .Where(x => userIds.Contains(x.UserID))
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.UserID);

                // Load analyst responses for this pillar
                var analystResponses = await _context.Assessments
                    .Where(a => mappingIds.Contains(a.UserCityMappingID)
                             && a.IsActive
                             && a.UpdatedAt.Year == year)
                    .SelectMany(a => a.PillarAssessments
                        .Where(pa => pa.PillarID == selectPillar.PillarID)
                        .SelectMany(pa => pa.Responses
                            .Where(r => r != null)
                            .Select(r => new HistoryQuestionAnswerRawDto
                            {
                                UserID = a.UserCityMapping.UserID,
                                QuestionID = r.QuestionID,
                                OptionID = r.QuestionOptionID,
                                ScoreValue = (int?)r.Score,
                                Progress = null,
                                Justification = r.Justification ?? "",
                                Source = r.Source ?? ""
                            })))
                    .AsNoTracking()
                    .ToListAsync();

                // Enrich analyst responses with full names
                foreach (var r in analystResponses)
                    r.FullName = users.TryGetValue(r.UserID, out var u) ? u?.FullName ?? "" : "";

                // Load AI estimated scores for this pillar
                var aiRawData = await _context.AIEstimatedQuestionScores
                    .Where(x => x.CityID == userCityMappings.CityID
                             && x.PillarID == selectPillar.PillarID
                             && x.Year == year)
                    .AsNoTracking()
                    .ToListAsync();

                var aiResponses = aiRawData.Select(x => new HistoryQuestionAnswerRawDto
                {
                    UserID = 0,
                    QuestionID = x.QuestionID,
                    OptionID = null,
                    ScoreValue = (int?)x.AIScore,
                    Progress = x.AIProgress ?? 0,
                    Justification = x.EvidenceSummary ?? "",
                    Source = $"{x.SourceDataExtract ?? ""} {x.SourceURL ?? ""}".Trim(),
                    FullName = "AI"
                }).ToList();

                // Combine analyst + AI responses and map onto each question's history
                var combined = analystResponses.Concat(aiResponses).ToList();

                foreach (var question in questions)
                {
                    var relatedEntries = combined.Where(x => x.QuestionID == question.QuestionID);

                    foreach (var entry in relatedEntries)
                    {
                        var option = question.QuestionOptions.FirstOrDefault(x => x.OptionID == entry.OptionID || x.ScoreValue == entry.ScoreValue); 
                        question.History.Add(new HistoryQuestionAnswerRawDto
                        {
                            UserID = entry.UserID,
                            FullName = entry.FullName,
                            ScoreValue = entry.ScoreValue,
                            Justification = entry.Justification,
                            OptionID = option?.OptionID,
                            OptionText = option?.OptionText ?? "",
                            Source = entry.Source
                        });
                    }
                }

                var result = new GetPillarQuestionByCityRespones
                {
                    AssessmentID = assessment?.AssessmentID ?? 0,
                    UserCityMappingID = request.UserCityMappingID,
                    PillarName = selectPillar.PillarName,
                    PillarID = selectPillar.PillarID,
                    Description = selectPillar.Description,
                    DisplayOrder = selectPillar.DisplayOrder,
                    SubmittedPillarDisplayOrder = answeredPillarIds.Count == 14
                                                   ? 14
                                                   : nextUnansweredPillar?.DisplayOrder ?? selectPillar.DisplayOrder,
                    Questions = questions
                };

                return ResultResponseDto<GetPillarQuestionByCityRespones>.Success(
                    result, new[] { "get questions successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetQuestionsByCityIdAsync", ex);
                return ResultResponseDto<GetPillarQuestionByCityRespones>.Failure(
                    new[] { "There is an error please try later" });
            }
        }

    }
}