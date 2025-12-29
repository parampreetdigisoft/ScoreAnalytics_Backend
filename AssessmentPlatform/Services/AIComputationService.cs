using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AssessmentPlatform.Services
{
    public class AIComputationService : IAIComputationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        public AIComputationService(ApplicationDbContext context, IAppLogger appLogger, Download download)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
        }

        public async Task<ResultResponseDto<List<AITrustLevel>>> GetAITrustLevels()
        {
            var r = await _context.AITrustLevels.ToListAsync();

            return ResultResponseDto<List<AITrustLevel>>.Success(r, new[] { "Pillar get successfully", });

        }

        public async Task<PaginationResponse<AiCitySummeryDto>> GetAICities(AiCitySummeryRequestDto request, int userID, UserRole userRole)
        {
            try
            {
                IQueryable<AiCitySummeryDto> query = await GetCityAiSummeryDetails(userID, userRole, request.CityID);

                return await query.ApplyPaginationAsync(request);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetCitiesAsync", ex);
                return new PaginationResponse<AiCitySummeryDto>();
            }
        }
        public async Task<IQueryable<AiCitySummeryDto>> GetCityAiSummeryDetails(int userID, UserRole userRole, int? cityID)
        {
            IQueryable<AICityScore> baseQuery = _context.AICityScores;

            if (userRole == UserRole.Analyst || userRole == UserRole.Evaluator)
            {
                // Allowed city IDs
                var allowedCityIds = cityID.HasValue
                    ? new List<int> { cityID.Value }   // <-- FIXED
                    : await _context.UserCityMappings
                            .Where(x => !x.IsDeleted && x.UserID == userID)
                            .Select(x => x.CityID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
            }
            else if (userRole == UserRole.CityUser)
            {
                var allowedCityIds = cityID.HasValue
                    ? new List<int> { cityID.Value }
                    : await _context.PublicUserCityMappings
                            .Where(x => x.IsActive && x.UserID == userID)
                            .Select(x => x.CityID)
                            .Distinct()
                            .ToListAsync();

                baseQuery = baseQuery.Where(x => allowedCityIds.Contains(x.CityID));
            }
            else
            {
                // Admin
                if (cityID.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.CityID == cityID.Value);
                }
            }

            var query = baseQuery
                .Include(x => x.City)
                .Select(x => new AiCitySummeryDto
                {
                    CityID = x.CityID,
                    State = x.City.State ?? "",
                    CityName = x.City.CityName ?? "",
                    Country = x.City.Country ?? "",
                    Image = x.City.Image ?? "",
                    ScoringYear = x.Year,
                    AIScore = x.AIScore,
                    AIProgress = x.AIProgress,
                    EvaluatorProgress = x.EvaluatorProgress,
                    Discrepancy = x.Discrepancy,
                    ConfidenceLevel = x.ConfidenceLevel,
                    EvidenceSummary = x.EvidenceSummary,
                    CrossPillarPatterns = x.CrossPillarPatterns,
                    InstitutionalCapacity = x.InstitutionalCapacity,
                    EquityAssessment = x.EquityAssessment,
                    SustainabilityOutlook = x.SustainabilityOutlook,
                    StrategicRecommendations = x.StrategicRecommendations,
                    DataTransparencyNote = x.DataTransparencyNote
                });

            return query;
        }
        public async Task<ResultResponseDto<AiCityPillarReponseDto>> GetAICityPillars(int cityID, int userID, UserRole userRole)
        {
            try
            {
                var res = await _context.AIPillarScores
                    .Where(x => x.CityID == cityID)
                    .Include(x => x.Pillar)
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
                var pillars = await _context.Pillars.ToListAsync();

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
                        PillarID = x.pillar.PillarID,
                        PillarName = x.pillar.PillarName,
                        Description = x.pillar.Description ?? "",
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
                    }
                    return r;
                })
                .OrderBy(x => !x.IsAccess)
                .ThenBy(x => x.DisplayOrder)
                .ToList();
                var trustLavels = await _context.AITrustLevels.ToListAsync();

                var finalResutl = new AiCityPillarReponseDto
                {
                    AITrustLevels = trustLavels,
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

                var res = _context.AIEstimatedQuestionScores
                    .Include(x => x.Question)
                    .Where(x => x.CityID == request.CityID && x.PillarID == request.PillarID)
                    .Select(x => new AIEstimatedQuestionScoreDto
                    {
                        CityID = x.CityID,
                        PillarID = x.PillarID,
                        QuestionID = x.QuestionID,
                        DataYear = x.Year,
                        AIScore = x.AIScore,
                        AIProgress = x.AIProgress,
                        EvaluatorProgress = x.EvaluatorProgress,
                        Discrepancy = x.Discrepancy,
                        ConfidenceLevel = x.ConfidenceLevel,
                        DataSourcesUsed = x.DataSourcesUsed,
                        EvidenceSummary = x.EvidenceSummary,
                        RedFlags = x.RedFlags,
                        GeographicEquityNote = x.GeographicEquityNote,
                        SourceType = x.SourceType,
                        SourceName = x.SourceName,
                        SourceURL = x.SourceURL,
                        SourceDataYear = x.SourceDataYear,
                        SourceDataExtract = x.SourceDataExtract,
                        SourceTrustLevel = x.SourceTrustLevel,
                        QuestionText = x.Question.QuestionText,

                    });

                var r = await res.ApplyPaginationAsync(request);

                return r;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GetAICityPillars", ex);
                return new PaginationResponse<AIEstimatedQuestionScoreDto>();
            }
        }


        public async Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto cityDetails)
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Element(ComposeHeader);
                        page.Content().Element(content => ComposeContent(content, cityDetails));
                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCityDetailsPdf", ex);
                return new byte[] { };
            }
        }

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().PaddingBottom(5).Text("AI Power City Details")
                        .FontSize(20)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    column.Item().Text(text =>
                    {
                        text.Span("Generated: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        text.Span(DateTime.Now.ToString("MMM dd, yyyy")).FontSize(9).Bold();
                    });
                });
            });
        }

        void ComposeContent(IContainer container, AiCitySummeryDto data)
        {
            container.Column(column =>
            {
                // City Header
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(data.CityName).FontSize(24).Bold();
                        col.Item().Text($"{data.State}, {data.Country}")
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(5).Text($"Scoring Year: {data.ScoringYear}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // AI Score Badge
                column.Item().PaddingVertical(10).Background(Colors.Yellow.Lighten3)
                    .Padding(10)
                    .Text($"AI Score: {data.AIProgress}/100")
                    .FontSize(14)
                    .Bold();

                // Confidence Level
                column.Item().PaddingTop(10).Row(row =>
                {
                    row.ConstantItem(120).Text("AI Confidence Level:")
                        .FontSize(11)
                        .Bold();
                    row.RelativeItem().Text(data.ConfidenceLevel)
                        .FontSize(11)
                        .FontColor(GetConfidenceColor(data.ConfidenceLevel));
                });

                // Progress Metrics
                column.Item().PaddingTop(15).Text("Progress Metrics")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Cell().Element(CellStyle).Text("AI Progress");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{data.AIProgress:F1}%");

                    table.Cell().Element(CellStyle).Text("Evaluator Progress");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{data.EvaluatorProgress:F1}%");

                    table.Cell().Element(CellStyle).Text("Discrepancy");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{data.Discrepancy:F1}%");

                    table.Cell().Element(CellStyle).Text("Average Progress");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{data.AIProgress:F2}%");
                });

                column.Item().PaddingTop(20).Text("AI Evidence Summary")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(5).Text(data.EvidenceSummary)
                    .FontSize(10)
                    .LineHeight(1.5f)
                    .Justify();

                // Category Summaries
                AddCategorySection(column, "Cross-Pillar Patterns", data.CrossPillarPatterns, true);
                AddCategorySection(column, "Institutional Capacity", data.InstitutionalCapacity,false);
                AddCategorySection(column, "Equity Assessment", data.EquityAssessment, false);
                AddCategorySection(column, "Sustainability Outlook", data.SustainabilityOutlook, true);
                AddCategorySection(column, "Strategic Recommendations", data.StrategicRecommendations, false);
                AddCategorySection(column, "Data Transparency Note", data.DataTransparencyNote, false);
            });
        }

        void AddCategorySection(ColumnDescriptor column, string title, string content, bool pageBreak = true)
        {
            if(pageBreak)
            column.Item().PageBreak();

            column.Item().PaddingTop(20).Background(Colors.Grey.Lighten3)
                .Padding(8)
                .Text(title)
                .FontSize(13)
                .Bold()
                .FontColor(Colors.Blue.Darken3);

            column.Item().PaddingTop(8).Text(content)
                .FontSize(10)
                .LineHeight(1.5f)
                .Justify();
        }

        static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(8);
        }

        static string GetConfidenceColor(string confidence)
        {
            return confidence.ToLower() switch
            {
                "high" => Colors.Green.Darken2,
                "medium" => Colors.Orange.Darken1,
                "low" => Colors.Red.Darken1,
                _ => Colors.Grey.Darken1
            };
        }
    }
}
