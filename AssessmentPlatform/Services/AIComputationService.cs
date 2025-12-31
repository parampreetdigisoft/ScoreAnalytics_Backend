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
                    .Include(x => x.City)
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
                        CityName = x.score?.City?.CityName ?? "",
                        State = x.score?.City?.State ?? "",
                        Country = x.score?.City?.Country ?? "",
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

                        page.Header().Element(PillarComposeHeader);
                        page.Content().Element(content => CitySummeryComposeContent(content, cityDetails));
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

        void CitySummeryComposeContent(IContainer container, AiCitySummeryDto data)
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
                AddCategorySection(column, "Institutional Capacity", data.InstitutionalCapacity, false);
                AddCategorySection(column, "Equity Assessment", data.EquityAssessment, false);
                AddCategorySection(column, "Sustainability Outlook", data.SustainabilityOutlook, true);
                AddCategorySection(column, "Strategic Recommendations", data.StrategicRecommendations, false);
                AddCategorySection(column, "Data Transparency Note", data.DataTransparencyNote, false);
            });
        }


        void AddCategorySection(ColumnDescriptor column, string title, string content, bool pageBreak = true)
        {
            if (pageBreak)
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
        public async Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarReponse pillarData)
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
                        page.Content().Element(content => PillarComposeContent(content, pillarData));
                        page.Footer().Element(PillarComposeFooter);
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsPdf", ex);
                return new byte[] { };
            }
        }

        void PillarComposeHeader(IContainer container, AiCityPillarReponse data)
        {
            container.Column(column =>
            {
                // Top Bar with Logo/Title
                column.Item().Background("#1E3A5F").Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Pillar Analysis Report")
                            .FontSize(16)
                            .FontColor(Colors.White);
                    });

                    row.ConstantItem(150).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text($"Generated")
                            .FontSize(9)
                            .FontColor("#B0C4DE");
                        col.Item().AlignRight().Text(DateTime.Now.ToString("MMM dd, yyyy"))
                            .FontSize(10)
                            .Bold()
                            .FontColor(Colors.White);
                    });
                });

                // Pillar Name Header
                column.Item().Background("#2C5F8D").Padding(12).Column(col =>
                {
                    col.Item().Text(data.PillarName)
                        .FontSize(22)
                        .Bold()
                        .FontColor(Colors.White);

                    col.Item().PaddingTop(3).Text($"{data.CityName},{data.State},{data.Country} | Data Year: {data.AIDataYear}")
                        .FontSize(10)
                        .FontColor("#E0E0E0");
                });
            });
        }

        void PillarComposeContent(IContainer container, AiCityPillarReponse data)
        {
            container.PaddingTop(8).Column(column =>
            {
                // Score Cards Row
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => PillarScoreCard(c, "AI Confidence",
                        data.ConfidenceLevel, GetConfidenceBadgeColor(data.ConfidenceLevel), true));
                    row.Spacing(10);

                    row.RelativeItem().Element(c => PillarScoreCard(c, "AI Score",
                        data.AIProgress!=null ? $"{data.AIProgress}%" : "N/A", "#FFC107", false));
                    row.Spacing(10);

                    row.RelativeItem().Element(c => PillarScoreCard(c, "Evaluator Score",
                        data.EvaluatorProgress != null ? $"{data.EvaluatorProgress}%" : "N/A", "#4CAF50", false));
                    row.Spacing(10);

                    row.RelativeItem().Element(c => PillarScoreCard(c, "Discrepancy",
                        $"{data.Discrepancy:F1}%", GetDiscrepancyColor(data.Discrepancy ?? 0), false));
                    row.Spacing(10);

                    row.RelativeItem().Element(c => PillarScoreCard(c, "Average Score",
                        $"{((data.AIProgress + data.EvaluatorProgress) ?? 0)/2:F0}%", "#2196F3", false));
                });

                // Progress Bars
                column.Item().PaddingTop(10).Element(c => PillarProgressSection(c, data));

                // Evidence Summary Section
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "AI Evidence Summary", data.EvidenceSummary, "#1976D2"));

                // Red Flags Section (with warning styling)
                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Red Flags", data.RedFlags, "#D32F2F"));

                // Other Sections
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Geographic Equity Note", data.GeographicEquityNote, "#388E3C"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Institutional Assessment", data.InstitutionalAssessment, "#7B1FA2"));

                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Data Gap Analysis", data.DataGapAnalysis, "#F57C00"));

                // Data Sources Section
                if (data.DataSourceCitations?.Any() == true)
                {
                    column.Item().PageBreak();
                    column.Item().PaddingTop(10).Element(c =>
                        DataSourcesSection(c, data.DataSourceCitations.ToList()));
                }

                // Description Section (HTML content handled as text)
                if (!string.IsNullOrEmpty(data.Description))
                {
                    column.Item().PageBreak();
                    column.Item().PaddingTop(10).Element(c =>
                        PillarContentSection(c, "Pillar Description",
                            StripHtmlTags(data.Description), "#455A64"));
                }
            });
        }

        void PillarScoreCard(IContainer container, string label, string value, string color, bool isBadge)
        {
            container.Background(Colors.White)
                .Border(1)
                .BorderColor("#E0E0E0")
                .Padding(10)
                .Column(column =>
                {
                    column.Item().Text(label)
                        .FontSize(10)
                        .FontColor("#757575")
                        .Bold();

                    if (isBadge)
                    {
                        column.Item().PaddingTop(8).Background(color)
                            .CornerRadius(12)
                            .Padding(6)
                            .AlignCenter()
                            .Text(value)
                            .FontSize(13)
                            .Bold()
                            .FontColor(Colors.Black);
                    }
                    else
                    {
                        column.Item().PaddingTop(8).Text(value)
                            .FontSize(16)
                            .Bold()
                            .FontColor(color);
                    }
                });
        }

        void PillarProgressSection(IContainer container, AiCityPillarReponse data)
        {
            container.Background(Colors.White)
                .Border(1)
                .BorderColor("#E0E0E0")
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("Progress Metrics")
                        .FontSize(16)
                        .Bold()
                        .FontColor("#1E3A5F");

                    column.Item().PaddingTop(12).Column(col =>
                    {
                        PillarProgressBar(col, "AI Progress", data.AIProgress, "#4CAF50");
                        col.Item().PaddingTop(10);
                        PillarProgressBar(col, "Evaluator Progress", data.EvaluatorProgress, "#2196F3");
                        col.Item().PaddingTop(10);
                        PillarProgressBar(col, "Discrepancy", data.Discrepancy, "#FF5722");
                    });
                });
        }

        void PillarProgressBar(ColumnDescriptor column, string label, decimal? percentage, string color)
        {
            var per  =  (float)(percentage ?? 0);
            column.Item().Row(row =>
            {
                row.ConstantItem(140).Text(label)
                    .FontSize(11)
                    .FontColor("#424242");

                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Height(20).Background("#F5F5F5").Row(barRow =>
                    {
                        barRow.RelativeItem(per ).Background(color);
                        barRow.RelativeItem(100 - per);
                    });
                });

                row.ConstantItem(55).AlignRight().Text($"{percentage:F1}%")
                    .FontSize(11)
                    .Bold()
                    .FontColor(color);
            });
        }

        void PillarContentSection(IContainer container, string title, string content, string accentColor)
        {
            container.Column(column =>
            {
                // Section Header with Accent Bar
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(accentColor);
                    row.RelativeItem().Background("#F5F5F5").Padding(12).Text(title)
                        .FontSize(15)
                        .Bold()
                        .FontColor("#212121");
                });

                // Content Box
                column.Item().Background(Colors.White)
                    .Border(1)
                    .BorderColor("#E0E0E0")
                    .Padding(18)
                    .Text(content)
                    .FontSize(10)
                    .LineHeight(1.6f)
                    .FontColor("#424242")
                    .Justify();
            });
        }

        void DataSourcesSection(IContainer container, List<AIDataSourceCitation> sources)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background("#1976D2");
                    row.RelativeItem().Background("#F5F5F5").Padding(12).Text("Data Source Citations")
                        .FontSize(15)
                        .Bold()
                        .FontColor("#212121");
                });

                column.Item().PaddingTop(10).Background(Colors.White)
                    .Border(1)
                    .BorderColor("#E0E0E0")
                    .Padding(15)
                    .Column(col =>
                    {
                        foreach (var source in sources.Take(10)) // Limit to first 10 for space
                        {
                            col.Item().PaddingBottom(15).Column(sourceCol =>
                            {
                                // Source Header
                                sourceCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(source.SourceName)
                                        .FontSize(11)
                                        .Bold()
                                        .FontColor("#1565C0");

                                    row.ConstantItem(100).AlignRight()
                                        .Background(GetSourceTypeBadgeColor(source.SourceType))
                                        .CornerRadius(3)
                                        .Padding(3)
                                        .Text(source.SourceType)
                                        .FontSize(8)
                                        .FontColor(Colors.White);
                                });

                                // Trust Level and Year
                                sourceCol.Item().PaddingTop(4).Row(row =>
                                {
                                    row.AutoItem().Text($"Trust Level: {source.TrustLevel}/7")
                                        .FontSize(9)
                                        .FontColor("#757575");

                                    row.AutoItem().PaddingLeft(15).Text($"Year: {source.DataYear}")
                                        .FontSize(9)
                                        .FontColor("#757575");
                                });

                                // Data Extract
                                if (!string.IsNullOrEmpty(source.DataExtract))
                                {
                                    sourceCol.Item().PaddingTop(6).Text(TruncateText(source.DataExtract, 200))
                                        .FontSize(9)
                                        .FontColor("#616161")
                                        .Italic();
                                }

                                // URL
                                if (!string.IsNullOrEmpty(source.SourceURL))
                                {
                                    sourceCol.Item().PaddingTop(4).Text(source.SourceURL)
                                        .FontSize(8)
                                        .FontColor("#1976D2")
                                        .Underline();
                                }
                            });

                            if (source != sources.Last())
                            {
                                col.Item().PaddingBottom(10).LineHorizontal(1).LineColor("#EEEEEE");
                            }
                        }
                    });
            });
        }

        void PillarComposeFooter(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                       
                    });
                    

                    col.Item().PaddingTop(5).AlignCenter().Text("AI Power City Assessment Platform")
                        .FontSize(8)
                        .FontColor("#9E9E9E");
                });
            });
        }

        void PillarComposeHeader(IContainer container)
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

        // Helper Methods
        static string GetConfidenceBadgeColor(string confidence) => confidence?.ToLower() switch
        {
            "high" => "#4CAF50",
            "medium" => "#FFC107",
            "low" => "#F44336",
            _ => "#9E9E9E"
        };

        static string GetDiscrepancyColor(decimal discrepancy) => discrepancy switch
        {
            < 10 => "#4CAF50",
            < 25 => "#FFC107",
            _ => "#F44336"
        };

        static string GetSourceTypeBadgeColor(string sourceType) => sourceType?.ToLower() switch
        {
            "government" => "#1976D2",
            "academic" => "#7B1FA2",
            "international" => "#00897B",
            "news/ngo" => "#F57C00",
            _ => "#616161"
        };

        static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        }

        static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }
    }
}
