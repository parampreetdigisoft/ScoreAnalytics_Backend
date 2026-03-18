// ═══════════════════════════════════════════════════════════════════════════
//  DocxGeneratorService.cs
//
//  NuGet package required (free):
//    DocumentFormat.OpenXml  >=  3.0.0   (MIT, by Microsoft)
//    SkiaSharp                            (already present via QuestPDF)
//
//  Architecture:
//   • Charts are rendered to PNG via SkiaSharp (reusing the same paint
//     methods used by PdfGeneratorService) and embedded as images.
//   • Text sections, progress bars, and data tables use native OpenXML.
//   • The IDocxGeneratorService interface mirrors IPdfGeneratorService so
//     the DocumentGeneratorService facade can swap them transparently.
// ═══════════════════════════════════════════════════════════════════════════

using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;

// Aliases to avoid clashes with System.Drawing / Wordprocessing
using A    = DocumentFormat.OpenXml.Drawing;
using DW   = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC  = DocumentFormat.OpenXml.Drawing.Pictures;
using QPDF = QuestPDF.Infrastructure;

namespace AssessmentPlatform.Common.Implementation
{
    public sealed partial class DocxGeneratorService : IDocxGeneratorService
    {
        // ── Constants ────────────────────────────────────────────────────────
        // All DXA values are "twentieths of a point" (1 inch = 1440 DXA).
        // All EMU values are "English Metric Units"  (1 inch = 914 400 EMU).

        private const uint   PageWidthDxa    = 11906;   // A4 width
        private const uint   PageHeightDxa   = 16838;   // A4 height
        private const int   MarginDxa       = 720;     // 0.5 inch margins
        private const int    ContentDxa      = (int)(PageWidthDxa - 2 * MarginDxa); // 10 466 DXA
        private const long   ContentWidthEmu = 6_645_000L;   // ≈ 7.27 inch in EMU
        private const long   HalfWidthEmu    = 3_220_000L;   // ≈ 3.52 inch in EMU
        private const string DarkGreen       = "134534";
        private const string MedGreen        = "336B58";
        private const string White           = "FFFFFF";

        // Unique image ID counter — reset per document
        private uint _imgId;

        private readonly IAppLogger _appLogger;

        public DocxGeneratorService(IAppLogger appLogger)
            => _appLogger = appLogger;

        // ════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<byte[]> GenerateCityDetailsDocx(
            AiCitySummeryDto cityDetails,
            List<AiCityPillarReponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCities,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    AddCityDetailsSections(body, mainPart, cityDetails, pillars, kpis, peerCities, userRole);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateCityDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsDocx(AiCityPillarReponse pillarData, UserRole userRole)
        {
            try
            {
                var cityDetails = new AiCitySummeryDto
                {
                    CityID = pillarData.CityID,
                    CityName = pillarData.CityName,
                    State = pillarData.State,
                    Country = pillarData.Country,
                    ScoringYear = pillarData.AIDataYear,
                    AIProgress = pillarData.AIProgress

                };

                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    AppendCityHeader(mainPart, cityDetails, pillarData.PillarName);
                    AddPillarSection(body, mainPart, pillarData, userRole);
                    FinalizeLastSection(mainPart);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GeneratePillarDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GenerateAllCitiesDetailsDocx(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarReponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    bool first = true;
                    foreach (var city in cities)
                    {
                        if (!pillarsDict.TryGetValue(city.CityID, out var pillars) || !pillars.Any())
                            continue;

                        var cityKpis = kpis?.Where(k => k.CityID == city.CityID).Take(109).ToList()
                                       ?? new List<KpiChartItem>();

                        if (!first) body.AppendChild(PageBreak());
                        first = false;

                        AddCityDetailsSections(body, mainPart, city, pillars, cityKpis,
                                               new List<PeerCityHistoryReportDto>(), userRole, isAllCities: true);
                    }
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateAllCitiesDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DOCUMENT SHELL HELPER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a blank A4 document, calls <paramref name="populate"/>, returns bytes.</summary>
        private byte[] BuildDocument(Action<MainDocumentPart> populate)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                populate(mainPart);

                // A4 page size + narrow margins + page numbers in footer
                var body = mainPart.Document.Body!;
                body.AppendChild(new SectionProperties(
                    new PageSize  { Width = PageWidthDxa, Height = PageHeightDxa },
                    new PageMargin { Top = MarginDxa, Right = MarginDxa,
                                     Bottom = MarginDxa, Left = MarginDxa }));

                mainPart.Document.Save();
            }
            return ms.ToArray();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CITY REPORT  –  SECTION COMPOSITION
        // ════════════════════════════════════════════════════════════════════
        private void AddCityDetailsSections(
            Body body, MainDocumentPart mainPart,
            AiCitySummeryDto cityDetails,
            List<AiCityPillarReponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCities,
            UserRole userRole,
            bool isAllCities = false)
        {
            // Reset pending header state for this document
            ResetSectionState();

            var kpiChartItems = kpis.Take(109).ToList();
            var pillarChartItems = pillars.Take(14)
                .Select(p => new PillarChartItem(
                    p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName ?? "—",
                    p.PillarName ?? "—",
                    p.AIProgress))
                .ToList();

            // ── 1. Global Dashboard ──────────────────────────────────────────────────
            if (!isAllCities)
            {
                AppendCityHeader(mainPart, cityDetails, "City Performance Dashboard");
                AddDashboardSection(body, mainPart, cityDetails, pillarChartItems, kpiChartItems);
            }

            // ── 2. City Summary ──────────────────────────────────────────────────────
            AppendCityHeader(mainPart, cityDetails, null);          
            AddCitySummarySection(body, mainPart, cityDetails, userRole);

            // ── 3. Pillar Radial Overview ────────────────────────────────────────────
            if (pillars.Any())
            {
                AppendCityHeader(mainPart, cityDetails, "Pillar Performance Overview");
                AddPillarOverviewSection(body, mainPart, pillarChartItems);
            }

            // ── 4. Peer Comparison & Trends ─────────────────────────────────────────
            if (!isAllCities && peerCities.Any())
            {
                AddPeerComparisonSections(body, mainPart, peerCities, cityDetails, userRole);
                AddPerformanceTrendSections(body, mainPart, peerCities, cityDetails, userRole);
            }

            // ── 5. Per-Pillar Detail ─────────────────────────────────────────────────
            var accessiblePillars = pillars.Where(x =>
                (x.IsAccess && userRole == UserRole.CityUser) || userRole != UserRole.CityUser).ToList();

            foreach (var pillar in accessiblePillars)
            {
                AppendCityHeader(mainPart, cityDetails, pillar.PillarName);
                AddPillarSection(body, mainPart, pillar, userRole);
            }

            // ── 6. KPI Dashboard (LAST section) ─────────────────────────────────────
            if (kpiChartItems.Any())
            {
                AppendCityHeader(mainPart, cityDetails, "KPI Dashboard");
                AddKpiDashboardSection(body, mainPart, kpiChartItems);
            }

            FinalizeLastSection(mainPart);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddDashboardSection(
            Body body, MainDocumentPart mainPart,
            AiCitySummeryDto city,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis)
        {
            float overall = (float)city.AIProgress.GetValueOrDefault();
            var validPillars = pillars.Where(p => p.Value.HasValue).ToList();

            // Row 1: Donut chart (left) + Radar chart (right)
            var donutPng  = RenderPng((c, s) => PaintDonut(c, s, overall), 320, 260);
            var radarPng  = RenderPng((c, s) => PaintSpiderChart(c, s, validPillars), 460, 260);
            body.AppendChild(CreateSideBySideImages(mainPart, donutPng, radarPng, 260));

            body.AppendChild(Gap(100));

            // Row 2: KPI stats band
            int green = kpis.Count(k => k.Value >= 70);
            int amber = kpis.Count(k => k.Value is >= 40 and < 70);
            int red   = kpis.Count(k => k.Value < 40);
            body.AppendChild(CreateKpiStatTable(kpis.Count, green, amber, red));

            // Highlight KPIs (UDRI / PRUPS)
            var topKpis = kpis.Where(x =>
                string.Equals(x.ShortName, "UDRI", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase)).ToList();
            if (topKpis.Any())
            {
                body.AppendChild(Gap(100));
                body.AppendChild(CreateKpiCardTable(mainPart, topKpis));
            }

            body.AppendChild(Gap(100));

            // Row 3: KPI Sparkline
            if (kpis.Any())
            {
                var sparkPng = RenderPng((c, s) => PaintKpiSparkline(c, s, kpis), 700, 130);
                body.AppendChild(CreateFullWidthImage(mainPart, sparkPng, 130));
            }

            
        }

        // ════════════════════════════════════════════════════════════════════
        //  CITY SUMMARY SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddCitySummarySection(
            Body body, MainDocumentPart mainPart,
            AiCitySummeryDto data, UserRole userRole)
        {
            // Progress metric
            body.AppendChild(SectionHeading("Progress Metrics", DarkGreen));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), MedGreen));
            body.AppendChild(Gap(160));

            // Text sections
            AppendContentSection(body, "Executive Summary",               data.EvidenceSummary,          "163329");
            body.AppendChild(PageBreak());
            AppendContentSection(body, "Cross-Pillar System Dynamics",    data.CrossPillarPatterns,      "6E9688");
            AppendContentSection(body, "Institutional Capacity Assessment", data.InstitutionalCapacity,  "0D8057");
            body.AppendChild(PageBreak());
            AppendContentSection(body, "Strategic Policy Priorities",     data.StrategicRecommendations, "2E9975");
            AppendContentSection(body, "Why This Assessment Matters",     data.DataTransparencyNote,     "63A68F");
        }

        // ════════════════════════════════════════════════════════════════════
        //  PILLAR OVERVIEW SECTION  (radial + horizontal bars)
        // ════════════════════════════════════════════════════════════════════

        private void AddPillarOverviewSection(
            Body body, MainDocumentPart mainPart,
            List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).Take(14).ToList();
            if (!data.Any()) return;

            var radialPng = RenderPng((c, s) => PaintPillarRadialChart(c, s, data), 360, 340);
            var barPng    = RenderPng((c, s) => PaintPillarHorizontalBars(c, s, data), 440, 340);
            body.AppendChild(CreateSideBySideImages(mainPart, radialPng, barPng, 340));
            body.AppendChild(Gap(160));
            body.AppendChild(CreatePillarFooterTable(data));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PER-PILLAR SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddPillarSection(
            Body body, MainDocumentPart mainPart,
            AiCityPillarReponse data, UserRole userRole)
        {
            body.AppendChild(SectionHeading("Progress Metrics", DarkGreen));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), MedGreen));
            body.AppendChild(Gap(160));

            AppendContentSection(body, "Evidence Summary",      data.EvidenceSummary,      "163329");
            body.AppendChild(PageBreak());
            AppendContentSection(body, "Red Flags",             data.RedFlags,             "ED561A");
            AppendContentSection(body, "Geographic Equity Note", data.GeographicEquityNote, "0D8057");
            body.AppendChild(PageBreak());
            AppendContentSection(body, "Institutional Assessment", data.InstitutionalAssessment, "2E9975");
            AppendContentSection(body, "Analytical Foundations",   data.DataGapAnalysis,         "A4BAB2");

            if (data.DataSourceCitations?.Any() == true)
            {
                body.AppendChild(PageBreak());
                AppendDataSourcesSection(body, data.DataSourceCitations.ToList());
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  KPI DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddKpiDashboardSection(
            Body body, MainDocumentPart mainPart,
            List<KpiChartItem> kpis)
        {
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int green = kpis.Count(x => x.Value >= 70);
            int amber = kpis.Count(x => x.Value is >= 40 and < 70);
            int red   = kpis.Count(x => x.Value < 40);
            float avg = (float)kpis.Average(x => x.Value ?? 0);

            body.AppendChild(CreateKpiSummaryBandTable(total, green, amber, red, avg));
            body.AppendChild(Gap(100));

            // Highlight KPIs
            var topKpis = kpis.Where(x =>
                string.Equals(x.ShortName, "UDRI",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase)).ToList();
            if (topKpis.Any())
            {
                body.AppendChild(CreateKpiCardTable(mainPart, topKpis));
                body.AppendChild(Gap(100));
            }

            // Groups of 18 KPIs — bar chart + interpretation cards
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 18)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();

            int offset = 0;
            foreach (var group in groups.Where(g => g.Any()))
            {
                int localOffset = offset;
                var barPng = RenderPng(
                    (c, s) => PaintKpiBarChart(c, s, group, localOffset),
                    700, 155);

                body.AppendChild(CreateFullWidthImage(mainPart, barPng, 155));
                body.AppendChild(Gap(80));
                body.AppendChild(CreateKpiCardTable(mainPart, group));
                body.AppendChild(Gap(160));
                offset += group.Count;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PEER COMPARISON SECTIONS  (Population, Regional, Income, Ranking)
        // ════════════════════════════════════════════════════════════════════

        private void AddPeerComparisonSections(
            Body body, MainDocumentPart mainPart,
            List<PeerCityHistoryReportDto> peerCities,
            AiCitySummeryDto cityDetails, UserRole userRole)
        {
            if (!peerCities.Any()) return;

            var main  = FindMainCity(peerCities, cityDetails);
            var peers = peerCities.Where(p => !IsSameCity(p.CityName, cityDetails.CityName)).ToList();
            var all   = BuildAllCities(main, peers);

            // Population comparison
            AppendCityHeader(mainPart, cityDetails, "Population-Based Peer Comparison");
            var popPng = RenderPng((c, s) => PaintPopulationBars(c, s, all, cityDetails), 700, all.Count * 34);
            body.AppendChild(CreateFullWidthImage(mainPart, popPng, all.Count * 34));
            body.AppendChild(Gap(100));
            body.AppendChild(CreateCityLegendTable(all, cityDetails));
            body.AppendChild(PageBreak());

            // Regional comparison
            AppendCityHeader(mainPart, cityDetails, "Regional Peer Group Comparison");
            var regionPng = RenderPng((c, s) => PaintRegionalBars(c, s, all), 700, 220);
            body.AppendChild(CreateFullWidthImage(mainPart, regionPng, 220));
            body.AppendChild(PageBreak());

            // Relative ranking
            AppendCityHeader(mainPart, cityDetails, "Relative Ranking Among Peer Cities");
            AddRankingSection(body, all, cityDetails);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PERFORMANCE TREND SECTIONS
        // ════════════════════════════════════════════════════════════════════

        private void AddPerformanceTrendSections(
            Body body, MainDocumentPart mainPart,
            List<PeerCityHistoryReportDto> peerCities,
            AiCitySummeryDto cityDetails, UserRole userRole)
        {
            if (!peerCities.Any()) return;

            var main  = FindMainCity(peerCities, cityDetails);
            var peers = peerCities.Where(p => !IsSameCity(p.CityName, cityDetails.CityName)).ToList();
            var all   = BuildAllCities(main, peers);

            var allYears = all
                .SelectMany(c => c.CityHistory ?? Enumerable.Empty<PeerCityYearHistoryDto>())
                .Select(h => h.Year).Distinct().OrderBy(y => y).ToList();

            if (!allYears.Any()) return;

            // Historical trend
            AppendCityHeader(mainPart, cityDetails, "Performance Trends Over Time");
            var peerAvg = allYears.Select(yr =>
            {
                var scores = peers.Select(p => p.CityHistory?.FirstOrDefault(h => h.Year == yr))
                    .Where(h => h != null).Select(h => (float)h!.ScoreProgress).ToList();
                return (Year: yr, Avg: scores.Any() ? scores.Average() : 0f, HasData: scores.Any());
            }).ToList();

            var trendPng = RenderPng(
                (c, s) => PaintMultiLineTrend(c, s, allYears, peers, main, cityDetails, peerAvg),
                700, 200);
            body.AppendChild(CreateFullWidthImage(mainPart, trendPng, 200));
            body.AppendChild(Gap(100));

            if (main != null)
                body.AppendChild(CreateYoYTable(allYears.TakeLast(5).ToList(),
                    (main.CityHistory ?? new()).OrderBy(h => h.Year).ToList(),
                    peerAvg.Select(p => (p.Year, p.Avg)).ToList()));

            body.AppendChild(PageBreak());

            // Pillar trend
            AppendCityHeader(mainPart, cityDetails, "Pillar-Level Trend Analysis");
            if (main != null)
            {
                var pillars = (main.CityHistory ?? new())
                    .SelectMany(h => h.Pillars ?? Enumerable.Empty<PeerCityPillarHistoryReportDto>())
                    .GroupBy(p => p.PillarID)
                    .Select(g => g.OrderBy(p => p.DisplayOrder).First())
                    .OrderBy(p => p.DisplayOrder).Take(14).ToList();

                if (pillars.Any())
                {
                    var pillarTrendPng = RenderPng(
                        (c, s) => PaintPillarLineChart(c, s, allYears, main.CityHistory ?? new(), pillars),
                        700, 200);
                    body.AppendChild(CreateFullWidthImage(mainPart, pillarTrendPng, 200));
                    body.AppendChild(Gap(100));
                    body.AppendChild(CreatePillarHeatmapTable(allYears, main.CityHistory ?? new(), pillars));
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RANKING SECTION  (inline, no new page needed)
        // ════════════════════════════════════════════════════════════════════

        private void AddRankingSection(
            Body body,
            List<PeerCityHistoryReportDto> all,
            AiCitySummeryDto cityDetails)
        {
            var ranked = all
                .Select(c => (City: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int mainRank = ranked.FindIndex(r => IsSameCity(r.City.CityName, cityDetails.CityName)) + 1;
            float mainScore = mainRank > 0 ? ranked[mainRank - 1].Score : 0f;

            // Hero banner
            body.AppendChild(CreateHeroBanner(cityDetails.CityName, mainRank, ranked.Count, mainScore));
            body.AppendChild(Gap(120));

            // Full ranking table
            body.AppendChild(SectionHeading("Full City Ranking", DarkGreen));
            var rows = ranked.Select((r, i) => new[]
            {
                (i + 1).ToString(),
                r.City.CityName,
                r.City.Country ?? "—",
                r.City.Region ?? "—",
                FormatPop(r.City.Population),
                $"{r.Score:F1}"
            }).ToArray();

            body.AppendChild(CreateStyledTable(
                new[] { "#", "City", "Country", "Region", "Pop.", "Score" },
                new[] { 360, 2000, 1300, 1400, 1000, 900 },
                rows,
                highlightRow: i => IsSameCity(ranked[i].City.CityName, cityDetails.CityName)));
            body.AppendChild(PageBreak());
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA SOURCES SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AppendDataSourcesSection(Body body, List<AIDataSourceCitation> sources)
        {
            body.AppendChild(SectionHeading("Data Source Citations", "396154"));
            foreach (var src in sources.Take(10))
            {
                body.AppendChild(BoldParagraph(src.SourceName ?? "", "2C423B", 22));
                body.AppendChild(NormalParagraph(
                    $"Trust Level: {src.TrustLevel}/7  |  Year: {src.DataYear}  |  Type: {src.SourceType ?? "—"}",
                    "757575", 18));
                if (!string.IsNullOrEmpty(src.DataExtract))
                    body.AppendChild(NormalParagraph(TruncateText(src.DataExtract, 200), "616161", 18, italic: true));
                if (!string.IsNullOrEmpty(src.SourceURL))
                    body.AppendChild(NormalParagraph(src.SourceURL, "305246", 16));
                body.AppendChild(Gap(120));
            }
        }


        /// <summary>
        /// Registers a repeating page header (appears on every page) that mirrors
        /// the QuestPDF CityComposeHeader layout:
        ///
        ///  ┌─────────────────────────────────────────┬────────────┐
        ///  │  [Title — bold white 21pt]              │ [  LOGO  ] │
        ///  │  City, State, Country | Data Year: YYYY │ [  white ] │
        ///  │  Generated: Mon DD, YYYY               │ [   box  ] │
        ///  └─────────────────────────────────────────┴────────────┘
        ///  ─────────── divider (#d9e2df) ───────────────────────────
        /// </summary>


        // ── Field: holds the pending header relId until the section is closed ──────
        private string? _pendingHeaderRelId = null;

        /// <summary>
        /// Call once before generating any city sections to reset state.
        /// </summary>
        private void ResetSectionState() => _pendingHeaderRelId = null;

        /// <summary>
        /// Creates a header for the upcoming section.
        /// Automatically closes the PREVIOUS section with a next-page section break
        /// (which replaces the manual PageBreak() call between sections).
        /// </summary>
        private void AppendCityHeader(
            MainDocumentPart mainPart,
            AiCitySummeryDto data,
            string? sectionTitle = null)
        {
            var body = mainPart.Document.Body!;

            // ── STEP 1: Finalize the PREVIOUS section ─────────────────────────────────
            // On every call except the first, seal the prior section by embedding
            // its sectPr inside a paragraph. This is how Word creates per-section
            // headers — the sectPr lives in the last paragraph of each section.
            if (_pendingHeaderRelId != null)
            {
                var closingSectPr = BuildSectionProperties(_pendingHeaderRelId);
                // This paragraph is the section divider — it forces a new page AND
                // carries the header reference for the section just finished.
                body.AppendChild(new Paragraph(new ParagraphProperties(closingSectPr)));
            }

            // ── STEP 2: Build the new header part ────────────────────────────────────
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();

            string title = string.IsNullOrEmpty(sectionTitle) ? data.CityName : sectionTitle;
            string logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/veridian-urban-index.png");

            int logoColW = 1200;
            int leftColW = ContentDxa - logoColW;

            // FIX – Logo EMU calculation
            // Column:   1200 DXA
            // Padding:  58 DXA each side → 116 DXA total
            // Drawable: 1084 DXA  →  1084 × 635 EMU/DXA = 688 340 EMU
            // Use 635 000 EMU (~1000 DXA) to leave a small visual margin
            const long logoEmu = 635_000L;

            var layoutTable = new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    // Fixed layout prevents Word from expanding columns past the page
                    new TableLayout { Type = TableLayoutValues.Fixed },
                    // FIX – Only border elements belong inside TableBorders.
                    // Margin elements were incorrectly nested here before.
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })));

            var row = new TableRow();

            // ── LEFT CELL ─────────────────────────────────────────────────────────────
            var leftCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = leftColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    HeaderCellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "134534" },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "172", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "172", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "172", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa })));

            leftCell.AppendChild(HeaderParagraph(title, "42", "FFFFFF", bold: true, spacingAfter: "40"));
            leftCell.AppendChild(HeaderParagraph(
                $"{data.CityName}, {data.State}, {data.Country} | Data Year: {data.ScoringYear}",
                "20", "E8F3F0", bold: false, spacingAfter: "20"));
            leftCell.AppendChild(HeaderParagraph(
                $"Generated: {DateTime.Now:MMM dd, yyyy}",
                "16", "CFE3DD", bold: false, spacingAfter: "0"));

            row.AppendChild(leftCell);

            // ── RIGHT CELL (white logo box) ───────────────────────────────────────────
            var rightCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = logoColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    HeaderCellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "FFFFFF" },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "58", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "58", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "58", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "58", Type = TableWidthUnitValues.Dxa })));

            if (File.Exists(logoPath))
            {
                byte[] logoBytes = File.ReadAllBytes(logoPath);
                var logoPara = EmbedImageInPart(headerPart, logoBytes, logoEmu, logoEmu);

                logoPara.ParagraphProperties ??= new ParagraphProperties();
                logoPara.ParagraphProperties.Justification =
                    new Justification { Val = JustificationValues.Center };
                logoPara.ParagraphProperties.SpacingBetweenLines =
                    new SpacingBetweenLines { Before = "0", After = "0" };

                rightCell.AppendChild(logoPara);
            }
            else
            {
                rightCell.AppendChild(new Paragraph());
            }

            row.AppendChild(rightCell);
            layoutTable.AppendChild(row);

            var divider = new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "0", After = "0" },
                    new ParagraphBorders(
                        new BottomBorder
                        {
                            Val = BorderValues.Single,
                            Size = 6,
                            Color = "d9e2df",
                            Space = 1
                        })));

            header.AppendChild(layoutTable);
            header.AppendChild(divider);
            headerPart.Header = header;
            header.Save();

            // ── STEP 3: Remember this relId — it will close the section when the
            //           NEXT AppendCityHeader call is made (or FinalizeLastSection). ──
            _pendingHeaderRelId = mainPart.GetIdOfPart(headerPart);
        }

        /// <summary>
        /// Must be called AFTER the last section's content has been appended.
        /// Attaches the last pending header to the document's final sectPr.
        /// </summary>
        private void FinalizeLastSection(MainDocumentPart mainPart)
        {
            if (_pendingHeaderRelId == null) return;

            var sectPr = mainPart.Document.Body!
                             .Elements<SectionProperties>().LastOrDefault()
                         ?? mainPart.Document.Body!.AppendChild(new SectionProperties());

            sectPr.RemoveAllChildren<HeaderReference>();
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Even, Id = _pendingHeaderRelId });
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.First, Id = _pendingHeaderRelId });
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = _pendingHeaderRelId });

            _pendingHeaderRelId = null;
        }

        /// <summary>
        /// Builds a SectionProperties with header references and a next-page break.
        /// </summary>
        private static SectionProperties BuildSectionProperties(string headerRelId)
        {
            var sp = new SectionProperties();
            sp.AppendChild(new SectionType { Val = SectionMarkValues.NextPage });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelId });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.First, Id = headerRelId });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.Even, Id = headerRelId });
            return sp;
        }
        // ── Helper: single-line paragraph for the header ─────────────────────────────
        private static Paragraph HeaderParagraph(
            string text,
            string fontSize,
            string color,
            bool bold,
            string spacingAfter)
        {
            var rp = new RunProperties(
                new Color { Val = color },
                new FontSize { Val = fontSize },
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
            if (bold) rp.PrependChild(new Bold());

            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "0", After = spacingAfter }),
                new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        // ── Helper: all-none TableCellBorders ─────────────────────────────────────────
        private static TableCellBorders HeaderCellNoBorders() =>
            new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

        // ── Helper: builds an inline image Drawing element ────────────────────────────

        

        /// <summary>
        /// Mirrors EmbedImage() exactly, but targets a <see cref="HeaderPart"/> instead of
        /// <see cref="MainDocumentPart"/> so the relationship is resolved inside the header.
        /// </summary>
        private Paragraph EmbedImageInPart(HeaderPart headerPart, byte[] pngBytes,
            long widthEmu, long heightEmu)
        {
            // Add image to the HeaderPart (not MainDocumentPart)
            var imgPart = headerPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = headerPart.GetIdOfPart(imgPart);   // relationship scoped to header.xml
            uint id = _imgId++;

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

            return new Paragraph(new Run(drawing));
        }
        /// <summary>Coloured section heading with accent left-border effect.</summary>
        private static Paragraph SectionHeading(string title, string hexColor)
        {
            return new Paragraph(
                new ParagraphProperties(

                    // ✅ Background
                    new Shading
                    {
                        Val = ShadingPatternValues.Clear,
                        Fill = "EAEAEA"
                    },

                    // ✅ Accent line (left)
                    new ParagraphBorders(
                        new LeftBorder
                        {
                            Val = BorderValues.Single,
                            Size = 24,
                            Color = hexColor
                        }
                    ),

                    // ✅ Proper spacing (top/bottom padding feel)
                    new SpacingBetweenLines
                    {
                        Before = "120",
                        After = "120"
                    },
                    new Tabs(
                    new TabStop
                    {
                        Val = TabStopValues.Left,
                        Position = 200
                    }
                )
                ),

                // ✅ Add manual left padding using tab
                new Run(
                    new TabChar(),
                    new RunProperties(
                        new Bold(),
                        new Color { Val = "2E2E2E" },
                        new FontSize { Val = "28" },
                        new RunFonts { Ascii = "Arial" }
                    ),
                    new Text(title)
                )
            );
        }

        /// <summary>Horizontal progress bar implemented as a two-cell table.</summary>
        private static Table CreateProgressBar(string label, float percentage, string hexColor)
        {
            percentage = Math.Clamp(percentage, 0f, 100f);
            int filled  = (int)(ContentDxa * percentage / 100f);
            int empty   = ContentDxa - filled;

            var border = new EnumValue<BorderValues>(BorderValues.None);
            var noBorders = new TableCellBorders(
                new TopBorder    { Val = border },
                new BottomBorder { Val = border },
                new LeftBorder   { Val = border },
                new RightBorder  { Val = border });

            // Label row
            var labelRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Color { Val = "424242" }, new FontSize { Val = "22" }),
                            new Text(label)))));

            // Bar row
            TableCell filledCell = filled > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = filled.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = hexColor },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            TableCell emptyCell = empty > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = empty.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F5F5F5" },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            var barRow = new TableRow(filledCell, emptyCell);
            barRow.AppendChild(new TableRowProperties(new TableRowHeight { Val = 300, HeightType = HeightRuleValues.Exact }));

            // Score label row
            var scoreRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = hexColor }, new FontSize { Val = "22" }),
                            new Text($"{percentage:F1}%")))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder   { Val = BorderValues.None })),
                labelRow, barRow, scoreRow);
        }

        /// <summary>Two-column content block: accent bar on left, title + body text on right.</summary>
        /// 
        private static void AppendContentSection(
    Body body, string title, string? content, string accentHex)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var paragraphs = content
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // ─────────────────────────────────────────
            // TABLE (Single column now)
            // ─────────────────────────────────────────
            var table = new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 6 }
                    )
                )
            );

            // ─────────────────────────────────────────
            // TITLE ROW (with accent line)
            // ─────────────────────────────────────────
            var titleCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "EAEAEA" },
                    new TableCellMargin(
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
                    ),
                    // ✅ Accent line ONLY here (left border trick)
                    new TableCellBorders(
                        new LeftBorder
                        {
                            Val = BorderValues.Single,
                            Size = 18, // thickness of accent line
                            Color = accentHex
                        }
                    )
                ),
                new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new Color { Val = "2E2E2E" },
                            new FontSize { Val = "28" }
                        ),
                        new Text(title)
                    )
                )
            );

            table.AppendChild(new TableRow(titleCell));

            // ─────────────────────────────────────────
            // CONTENT ROW
            // ─────────────────────────────────────────
            var contentCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "F7F7F7" },
                    new TableCellMargin(
                        new TopMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            foreach (var para in paragraphs)
            {
                contentCell.AppendChild(new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Both },
                        new SpacingBetweenLines
                        {
                            Line = "276", // ✅ 1.15
                            LineRule = LineSpacingRuleValues.Auto,
                            After = "120"
                        }
                    ),
                    new Run(
                        new RunProperties(
                            new Color { Val = "444444" },
                            new FontSize { Val = "22" }
                        ),
                        new Text(para) { Space = SpaceProcessingModeValues.Preserve }
                    )
                ));
            }

            table.AppendChild(new TableRow(contentCell));

            body.AppendChild(table);

            body.AppendChild(Gap(140));
        }        

        // ── KPI stat band (4 colored boxes) ─────────────────────────────────

        private static Table CreateKpiStatTable(int total, int green, int amber, int red)
        {
            int cellW = ContentDxa / 4;
            TableCell Stat(string val, string label, string bg, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);
                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                        new TableCellMargin(
                            new TopMargin    { Width = "80",  Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "80",  Type = TableWidthUnitValues.Dxa },
                            new LeftMargin   { Width = "120", Type = TableWidthUnitValues.Dxa },
                            new RightMargin  { Width = "120", Type = TableWidthUnitValues.Dxa }),
                        new TableCellBorders(
                            new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = fg }, new FontSize { Val = "40" }),
                            new Text(val))),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Color { Val = fg }, new FontSize { Val = "14" }),
                            new Text(label))));
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Stat(green.ToString(),  "Performing ≥70%",    "E8F5E9", "2E7D32"),
                    Stat(amber.ToString(),  "Developing 40–69%",  "FFF8E1", "E65100"),
                    Stat(red.ToString(),    "Needs Improvement",  "FDECEA", "C62828"),
                    Stat(total.ToString(),  "Total KPIs",         "EEF5F1", "12352F")));
        }

        // ── KPI summary band (dark green strip) ──────────────────────────────

        private static Table CreateKpiSummaryBandTable(
            int total, int green, int amber, int red, float avg)
        {
            int cellW = ContentDxa / 5;
            string avgColor = avg >= 70 ? "4CAF50" : avg >= 40 ? "FFC107" : "EF5350";

            TableCell Pill(string val, string label, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);
                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" },
                        new TableCellBorders(
                            new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = fg }, new FontSize { Val = "30" }),
                            new Text(val))),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Color { Val = "FFFFFFBB" }, new FontSize { Val = "13" }),
                            new Text(label))));
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Pill(total.ToString(),  "Total KPIs",        "4CAF50"),
                    Pill(green.ToString(),  "Performing ≥70%",   "4CAF50"),
                    Pill(amber.ToString(),  "Developing 40–69%", "FFC107"),
                    Pill(red.ToString(),    "Needs Improvement", "EF5350"),
                    Pill($"{avg:F1}%",      "Average Score",     avgColor)));
        }

        // ── KPI card grid (2 per row, with interpretation table) ─────────────

        private Table CreateKpiCardTable(MainDocumentPart mainPart, List<KpiChartItem> kpis)
        {
            int gap = 120;
            int cardW = (ContentDxa - gap) / 2;

            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var pairs = kpis
                .Select((k, i) => (k, i))
                .GroupBy(t => t.i / 2)
                .Select(g => g.ToList())
                .ToList();

            int globalIdx = 0;
            foreach (var pair in pairs)
            {
                var row = new TableRow();

                for (int pIdx = 0; pIdx < pair.Count; pIdx++)
                {
                    var (kpi, localI) = pair[pIdx];
                    int cardNum = globalIdx + localI + 1;

                    decimal v = kpi.Value ?? 0;
                    v = v == 100 ? Math.Round(v, 0) : Math.Round(v, 1);
                    string accent = GetBarColor((float)v).TrimStart('#');

                    var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
                    var matched = interps.FirstOrDefault(x =>
                        x.MinRange.HasValue && x.MaxRange.HasValue &&
                        v >= x.MinRange.Value && v <= x.MaxRange.Value);

                    if (matched == null && interps.Any())
                        matched = interps
                            .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                            .OrderBy(x => Math.Min(
                                Math.Abs(v - x.MinRange!.Value),
                                Math.Abs(v - x.MaxRange!.Value)))
                            .FirstOrDefault();

                    var cardTable = BuildKpiCardTable(kpi, cardNum, v, accent, interps, matched, cardW);

                    // Wrap card table in a cell
                    var cell = new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        cardTable);
                    row.AppendChild(cell);

                    // Gap cell between the two cards
                    if (pair.Count > 1 && pIdx == 0)
                        row.AppendChild(new TableCell(
                            new TableCellProperties(
                                new TableCellWidth { Width = gap.ToString(), Type = TableWidthUnitValues.Dxa },
                                CellNoBorders()),
                            new Paragraph()));
                }

                // Pad to keep layout when row has only 1 card
                if (pair.Count == 1)
                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        new Paragraph()));

                table.AppendChild(row);
                table.AppendChild(new TableRow(SpacerCell(ContentDxa, 80)));
                globalIdx += pair.Count;
            }

            return table;
        }

        /// <summary>
        /// Builds a single KPI card as a nested table, matching the QuestPDF card layout:
        ///   ┌─────────────────────────────────────────┐  ← accent border
        ///   │  [#N]  ShortName            score%       │  ← accent bg header
        ///   │         Name (subtitle)                  │
        ///   ├──────────┬──────────────────────────────┤  ← #F0F0F0 sub-header
        ///   │  Range   │ Condition                     │
        ///   ├──────────┼──────────────────────────────┤
        ///   │  0–20    │ Very Low                      │  ← stripe rows, matched = accent
        ///   │  …       │ …                             │
        ///   └──────────┴──────────────────────────────┘
        /// </summary>
        private Table BuildKpiCardTable(
            KpiChartItem kpi,
            int cardNum,
            decimal v,
            string accent,
            List<FiveLevelInterpretationsDto> interps,
            FiveLevelInterpretationsDto? matched,
            int cardW)
        {
            int rangeColW = 920;
            int condColW = cardW - rangeColW;

            var card = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            // ── ROW 1: Header band ───────────────────────────────────────────────────
            int bubbleW = 260;
            int scoreW = 720;
            int nameW = cardW - bubbleW - scoreW;

            var headerInner = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var hRow = new TableRow();

            // Number bubble
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = bubbleW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "13" }),
                        new Text(cardNum.ToString())))));

            // ShortName + Name
            var nameCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = nameW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                    new Text(kpi.ShortName ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Color { Val = "DDDDDD" }, new FontSize { Val = "11" }),
                    new Text(kpi.Name ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            hRow.AppendChild(nameCell);

            // Score
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = scoreW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "20" }),
                        new Text($"{v}%") { Space = SpaceProcessingModeValues.Preserve }))));

            headerInner.AppendChild(hRow);

            var headerRow = new TableRow();
            headerRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new GridSpan { Val = 2 },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent }),
                headerInner));
            card.AppendChild(headerRow);

            // ── ROW 2: Definition strip (only when Definition has content) ───────────
            if (!string.IsNullOrWhiteSpace(kpi.Definition))
            {
                // "DEF" label + definition text share one paragraph with a run each
                var defPara = new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "0", After = "0" }));

                // "DEF " label — small, bold, accent colour
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Bold(),
                        new Color { Val = accent },
                        new FontSize { Val = "9" },          // 4.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text("DEF :  ") { Space = SpaceProcessingModeValues.Preserve }));

                // Definition body — italic, dark grey, wraps naturally
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Italic(),
                        new Color { Val = "444444" },
                        new FontSize { Val = "11" },          // 5.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text(kpi.Definition) { Space = SpaceProcessingModeValues.Preserve }));

                var defCell = new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new GridSpan { Val = 2 },               // spans Range + Condition columns
                        new TableCellBorders(
                            new TopBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = accent,
                                Space = 0
                            },
                            new BottomBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = "DDDDDD",
                                Space = 0
                            },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None }),
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F6F4" },
                        new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                        new TableCellMargin(
                            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                    defPara);

                var defRow = new TableRow();
                defRow.AppendChild(defCell);
                card.AppendChild(defRow);
            }

            // ── ROW 3: Sub-header (Range | Condition) ────────────────────────────────
            var subHdrRow = new TableRow();
            subHdrRow.AppendChild(MakeInterpCell("Range", rangeColW, "F0F0F0", "666666", "11", bold: true, bottomDivider: false));
            subHdrRow.AppendChild(MakeInterpCell("Condition", condColW, "F0F0F0", "666666", "11", bold: true, bottomDivider: false));
            card.AppendChild(subHdrRow);

            // ── ROWS 4+: Interpretation rows ─────────────────────────────────────────
            for (int i = 0; i < interps.Count; i++)
            {
                var interp = interps[i];
                bool isHit = interp == matched;
                bool isLast = i == interps.Count - 1;

                string rowBg = isHit ? accent : (i % 2 == 0 ? "FFFFFF" : "F7F7F7");
                string rangeFg = isHit ? White : "888888";
                string condFg = isHit ? White : "333333";

                string rangeStr = interp.MinRange.HasValue && interp.MaxRange.HasValue
                    ? $"{Math.Round(interp.MinRange.Value, 0)}–{Math.Round(interp.MaxRange.Value, 0)}"
                    : "—";

                var interpRow = new TableRow();
                interpRow.AppendChild(MakeInterpCell(rangeStr, rangeColW, rowBg, rangeFg, "12", bold: false, bottomDivider: !isLast));
                interpRow.AppendChild(MakeInterpCell(interp.Condition ?? "—", condColW, rowBg, condFg, "13", bold: isHit, bottomDivider: !isLast));
                card.AppendChild(interpRow);
            }

            return card;
        }

        /// <summary>Creates a single interpretation table cell.</summary>
        private static TableCell MakeInterpCell(
            string text,
            int width,
            string bgColor,
            string fgColor,
            string fontSize,
            bool bold,
            bool bottomDivider)
        {
            var borders = new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new BottomBorder
                {
                    Val = bottomDivider ? BorderValues.Single : BorderValues.None,
                    Size = 2,
                    Color = "E0E0E0"
                });

            var rp = new RunProperties(
                new Color { Val = fgColor },
                new FontSize { Val = fontSize });
            if (bold) rp.AppendChild(new Bold());

            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                    borders,
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bgColor },
                    new TableCellMargin(
                        new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        }

        /// <summary>Returns a TableCellBorders with all sides set to None.</summary>
        private static TableCellBorders CellNoBorders() =>
            new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

        // ── Pillar footer band (avg, best, worst) ────────────────────────────

        private static Table CreatePillarFooterTable(List<PillarChartItem> data)
        {
            float avg   = (float)data.Average(x => x.Value ?? 0);
            var   best  = data.OrderByDescending(x => x.Value).First();
            var   worst = data.OrderBy(x => x.Value).First();
            int   w3    = ContentDxa / 3;
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);

            TableCell Cell(string[] lines, string[] fgs, string bg)
            {
                var noBord = new TableCellBorders(
                    new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                    new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder });
                var tc = new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = w3.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                    noBord));
                for (int i = 0; i < lines.Length; i++)
                    tc.AppendChild(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                            new Color { Val = fgs[i] },
                            new FontSize { Val = i == 0 ? "40" : "18" }),
                            new Text(lines[i]))));
                return tc;
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Cell(new[] { $"{avg:F1}%", "Average Score" },
                         new[] { GetBarColor(avg).TrimStart('#'), "A5D6A7" }, "12352F"),
                    Cell(new[] { $"▲ {Shorten(best.Name ?? "—", 22)}", $"{best.Value:F1}%" },
                         new[] { "1B5E20", "2E7D32" }, "E8F5E9"),
                    Cell(new[] { $"▼ {Shorten(worst.Name ?? "—", 22)}", $"{worst.Value:F1}%" },
                         new[] { "B71C1C", "C62828" }, "FDECEA")));
        }

        // ── Hero ranking banner ───────────────────────────────────────────────

        private static Table CreateHeroBanner(
            string cityName, int rank, int total, float score)
        {
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            var noBorders = new TableCellBorders(
                new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder });

            var leftCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = (ContentDxa - 1800).ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" },
                    noBorders.CloneNode(true)),
                new Paragraph(
                    new ParagraphProperties(
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Run(new RunProperties(new Bold(), new Color { Val = "F0B429" }, new FontSize { Val = "64" }),
                        new Text($"#{rank} of {total}"))),
                new Paragraph(
                    new ParagraphProperties(
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Run(new RunProperties(new Color { Val = "A5D6C2" }, new FontSize { Val = "22" }),
                        new Text(cityName))));

            var rightCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = "1800", Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" },
                    noBorders.CloneNode(true)),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Run(new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "56" }),
                        new Text($"{score:F1}"))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(leftCell, rightCell));
        }

        // ── City legend table ────────────────────────────────────────────────

        private static Table CreateCityLegendTable(
            List<PeerCityHistoryReportDto> allCities, AiCitySummeryDto cityDetails)
        {
            string[] palette = { "F0B429", "4CAF8A", "1E88E5", "FB8C00", "7B61FF", "E05252" };
            var rows = new List<string[]>();
            for (int i = 0; i < allCities.Count; i++)
            {
                bool isMain = IsSameCity(allCities[i].CityName, cityDetails.CityName);
                rows.Add(new[] { isMain ? "★" : "•", allCities[i].CityName, allCities[i].Country ?? "—" });
            }
            return CreateStyledTable(
                new[] { "", "City", "Country" },
                new[] { 300, 4000, 2000 },
                rows.ToArray());
        }

        // ── General styled data table ─────────────────────────────────────────

        private static Table CreateStyledTable(
            string[] headers, int[] colWidthsDxa, string[][] rows,
            Func<int, bool>? highlightRow = null)
        {
            var borderSingle = new EnumValue<BorderValues>(BorderValues.Single);
            TableCellBorders DataBorders() => new TableCellBorders(
                new BottomBorder { Val = borderSingle, Size = 4, Color = "E0E0E0" });

            var table = new Table(new TableProperties(
                new TableWidth { Width = colWidthsDxa.Sum().ToString(), Type = TableWidthUnitValues.Dxa }));

            // Header row
            var hRow = new TableRow();
            for (int c = 0; c < headers.Length; c++)
            {
                hRow.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                            new Text(headers[c])))));
            }
            table.AppendChild(hRow);

            // Data rows
            for (int r = 0; r < rows.Length; r++)
            {
                bool highlight = highlightRow?.Invoke(r) ?? false;
                string rowBg = highlight ? "FFF9E6" : (r % 2 == 0 ? "FFFFFF" : "FAFAFA");
                var dRow = new TableRow();
                for (int c = 0; c < rows[r].Length && c < headers.Length; c++)
                {
                    dRow.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = rowBg },
                            DataBorders()),
                        new Paragraph(
                            new Run(new RunProperties(
                                new Color { Val = highlight ? "12352F" : "333333" },
                                new FontSize { Val = "16" }),
                                new Text(rows[r][c])))));
                }
                table.AppendChild(dRow);
            }
            return table;
        }

        // ── YoY performance table ─────────────────────────────────────────────

        private static Table CreateYoYTable(
            List<int> years,
            List<PeerCityYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            int yearW = (ContentDxa - 1300) / Math.Max(years.Count, 1);
            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            TableCell HdrCell(string txt, bool first = false) =>
                new(new TableCellProperties(
                    new TableCellWidth { Width = (first ? 1300 : yearW).ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Paragraph(new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                        new Text(txt))));

            var hRow = new TableRow();
            hRow.AppendChild(HdrCell("Metric", first: true));
            foreach (var yr in years) hRow.AppendChild(HdrCell(yr.ToString()));
            table.AppendChild(hRow);

            void DataRow(string label, Func<int, string> valFn, Func<int, string> colorFn, string bg)
            {
                var row = new TableRow();
                row.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = "1300", Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                    new Paragraph(new Run(
                        new RunProperties(new Color { Val = "333333" }, new FontSize { Val = "16" }),
                        new Text(label)))));
                foreach (var yr in years)
                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                            new Run(new RunProperties(
                                new Bold(), new Color { Val = colorFn(yr) }, new FontSize { Val = "16" }),
                                new Text(valFn(yr))))));
                table.AppendChild(row);
            }

            DataRow("Score",
                yr => { float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0); return $"{s:F1}"; },
                yr => { float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0); return GetBarColor(s).TrimStart('#'); },
                "F4F7F5");

            DataRow("YoY Δ",
                yr =>
                {
                    int idx = years.IndexOf(yr);
                    if (idx == 0) return "—";
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[idx - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float d = curr - prev; return $"{(d >= 0 ? "+" : "")}{d:F1}";
                },
                yr =>
                {
                    int idx = years.IndexOf(yr);
                    if (idx == 0) return "888888";
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[idx - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    return curr >= prev ? "336B58" : "E05252";
                },
                "FFFFFF");

            DataRow("vs Peers",
                yr =>
                {
                    float mine = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float peer = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    float d = mine - peer; return $"{(d >= 0 ? "+" : "")}{d:F1}";
                },
                yr =>
                {
                    float mine = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float peer = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    return mine >= peer ? "336B58" : "E05252";
                },
                "F4F7F5");

            return table;
        }

        // ── Pillar heatmap table ──────────────────────────────────────────────

        private static Table CreatePillarHeatmapTable(
            List<int> allYears,
            List<PeerCityYearHistoryDto> history,
            List<PeerCityPillarHistoryReportDto> pillars)
        {
            int yearW   = Math.Max(400, (ContentDxa - 1600) / Math.Max(allYears.Count, 1));
            var table   = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            // Header
            var hRow = new TableRow();
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = "1600", Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                new Paragraph(new Run(
                    new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                    new Text("Pillar")))));
            foreach (var yr in allYears)
                hRow.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "12352F" }),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "14" }),
                            new Text(yr.ToString())))));
            table.AppendChild(hRow);

            // Data rows
            foreach (var (pillar, pi) in pillars.Select((p, i) => (p, i)))
            {
                string rowBg = pi % 2 == 0 ? "F4F7F5" : "FFFFFF";
                var row = new TableRow();
                row.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = "1600", Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = rowBg }),
                    new Paragraph(new Run(
                        new RunProperties(new Bold(), new Color { Val = "12352F" }, new FontSize { Val = "14" }),
                        new Text(pillar.PillarName)))));

                foreach (var yr in allYears)
                {
                    var h  = history.FirstOrDefault(h2 => h2.Year == yr);
                    var ps = h?.Pillars?.FirstOrDefault(p2 => p2.PillarID == pillar.PillarID);
                    bool hasData = ps != null;
                    float score  = hasData ? (float)ps!.ScoreProgress : -1f;
                    string cellBg = !hasData ? "F0F0F0"
                        : InterpolateColor("FFFFFF", "12352F", score / 100f).TrimStart('#');

                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = cellBg }),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                            new Run(new RunProperties(
                                new Color { Val = score >= 50 ? White : "333333" },
                                new FontSize { Val = "14" }),
                                new Text(!hasData ? "—" : $"{score:F1}")))));
                }
                table.AppendChild(row);
            }
            return table;
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMAGE EMBEDDING HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Embeds a PNG byte-array as a full-width image in the document.
        /// <paramref name="naturalHeightPx"/> is used to compute the aspect ratio.
        /// </summary>
        private Paragraph CreateFullWidthImage(
            MainDocumentPart mainPart, byte[] pngBytes, int naturalHeightPx,
            int naturalWidthPx = 700)
        {
            long widthEmu  = ContentWidthEmu;
            long heightEmu = ContentWidthEmu * naturalHeightPx / naturalWidthPx;
            return EmbedImage(mainPart, pngBytes, widthEmu, heightEmu);
        }

        /// <summary>Creates a two-cell table, each half containing one image.</summary>
        private Table CreateSideBySideImages(
            MainDocumentPart mainPart,
            byte[] leftPng, byte[] rightPng,
            int naturalHeightPx)
        {
            long hw     = HalfWidthEmu;
            long hh     = hw * naturalHeightPx / 320; // approx aspect

            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            TableCell ImgCell(byte[] png) => new(
                new TableCellProperties(
                    new TableCellWidth { Width = (ContentDxa / 2).ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableCellBorders(
                        new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                        new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                EmbedImage(mainPart, png, hw, hh));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(ImgCell(leftPng), ImgCell(rightPng)));
        }

        private Paragraph EmbedImage(
            MainDocumentPart mainPart, byte[] pngBytes, long widthEmu, long heightEmu)
        {
            var imgPart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = mainPart.GetIdOfPart(imgPart);
            uint id = _imgId++;

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                        { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

            return new Paragraph(new Run(drawing));
        }

        // ════════════════════════════════════════════════════════════════════
        //  SKIA CHART RENDERERS  →  PNG bytes
        //  All Paint* methods below replicate the logic from PdfGeneratorService
        //  but operate on a SkiaSharp surface instead of a QuestPDF canvas.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Renders any SkiaSharp paint action to a PNG byte array.</summary>
        private static byte[] RenderPng(
            Action<SKCanvas, QPDF.Size> paintAction,
            int width, int height)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            paintAction(canvas, new QPDF.Size(width, height));
            canvas.Flush();
            using var snap    = surface.Snapshot();
            using var encoded = snap.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        // Forward declarations — these call the SAME static paint methods used by PdfGeneratorService.
        // If PdfGeneratorService makes them internal/protected, call them directly; otherwise just
        // redeclare the tiny wrappers below.

        private static void PaintDonut(SKCanvas c, QPDF.Size s, float score) =>
            PdfGeneratorService.PaintDonutPublic(c, s, score);

        private static void PaintSpiderChart(SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.PaintSpiderChartPublic(c, s, pillars);

        private static void PaintKpiSparkline(SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis) =>
            PdfGeneratorService.PaintKpiSparklinePublic(c, s, kpis);

        private static void PaintKpiBarChart(
            SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis, int offset) =>
            PdfGeneratorService.DrawKpiBarChartCanvas(c, s, kpis, offset);

        private static void PaintPillarRadialChart(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarsRadialChartCanvas(c, s, pillars);

        private static void PaintPillarHorizontalBars(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarHorizontalBarsCanvas(c, s, pillars);

        private static void PaintPopulationBars(
            SKCanvas c, QPDF.Size s,
            List<PeerCityHistoryReportDto> cities, AiCitySummeryDto cityDetails) =>
            PdfGeneratorService.DrawPopulationBarsCanvas(c, s, cities, cityDetails);

        private static void PaintRegionalBars(
            SKCanvas c, QPDF.Size s, List<PeerCityHistoryReportDto> all) =>
            PdfGeneratorService.DrawRegionalBarsCanvas(c, s, all);

        private static void PaintMultiLineTrend(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main, AiCitySummeryDto details,
            List<(int Year, float Avg, bool HasData)> avg) =>
            PdfGeneratorService.DrawMultiLineTrendChartCanvas(c, s, years, peers, main, details, avg);

        private static void PaintPillarLineChart(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCityYearHistoryDto> history,
            List<PeerCityPillarHistoryReportDto> pillars) =>
            PdfGeneratorService.DrawPillarLineChartCanvas(c, s, years, history, pillars);

        // ════════════════════════════════════════════════════════════════════
        //  PARAGRAPH / ELEMENT UTILITIES
        // ════════════════════════════════════════════════════════════════════

        private static Paragraph NormalParagraph(
            string text, string hexColor, int halfPtSize,
            bool italic = false, string bg = "FFFFFF")
        {
            var rPr = new RunProperties(
                new Color { Val = hexColor },
                new FontSize { Val = halfPtSize.ToString() },
                new RunFonts { Ascii = "Arial" });
            if (italic) rPr.AppendChild(new Italic());

            return new Paragraph(
                new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static Paragraph BoldParagraph(string text, string hexColor, int halfPtSize) =>
            new(new Run(
                new RunProperties(
                    new Bold(), new Color { Val = hexColor },
                    new FontSize { Val = halfPtSize.ToString() },
                    new RunFonts { Ascii = "Arial" }),
                new Text(text)));

        /// <summary>Empty paragraph with controlled spacing (spacing in twentieths of a point).</summary>
        private static Paragraph Gap(int spacingAfter) =>
            new(new ParagraphProperties(new SpacingBetweenLines { After = spacingAfter.ToString() }));

        private static Paragraph PageBreak() =>
            new(new Run(new Break { Type = BreakValues.Page }));


        private static TableCell SpacerCell(int widthDxa, uint heightTwips) =>
            new(new TableCellProperties(
                    new TableCellWidth { Width = widthDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = heightTwips.ToString() })));

        // ════════════════════════════════════════════════════════════════════
        //  COLOUR / FORMAT UTILITIES  (mirrors PdfGeneratorService statics)
        // ════════════════════════════════════════════════════════════════════

        private static string GetBarColor(float value) =>
            value >= 70 ? "#2E7D32" : value >= 40 ? "#F9A825" : "#C62828";

        private static string Shorten(string text, int max) =>
            string.IsNullOrWhiteSpace(text) ? "" :
            text.Length <= max ? text : text[..max] + "…";

        private static string TruncateText(string text, int maxLength) =>
            string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text : text[..maxLength] + "...";

        private static string InterpolateColor(string from, string to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var c1 = SKColor.Parse(from);
            var c2 = SKColor.Parse(to);
            byte r = (byte)(c1.Red   + (c2.Red   - c1.Red)   * t);
            byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
            byte b = (byte)(c1.Blue  + (c2.Blue  - c1.Blue)  * t);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static float GetLatestScoreOrZero(PeerCityHistoryReportDto city) =>
            city.CityHistory?.OrderByDescending(h => h.Year).FirstOrDefault() is { } last
                ? (float)last.ScoreProgress : -1f;

        private static PeerCityHistoryReportDto? FindMainCity(
            List<PeerCityHistoryReportDto> all, AiCitySummeryDto city) =>
            all.FirstOrDefault(p => IsSameCity(p.CityName, city.CityName));

        private static bool IsSameCity(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static List<PeerCityHistoryReportDto> BuildAllCities(
            PeerCityHistoryReportDto? main, List<PeerCityHistoryReportDto> peers)
        {
            var list = new List<PeerCityHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }

        private static string FormatPop(decimal? value)
        {
            if (!value.HasValue || value <= 0) return "N/A";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000M:F1}B";
            if (value >= 1_000_000)     return $"{value / 1_000_000M:F1}M";
            if (value >= 1_000)         return $"{value / 1_000M:F0}K";
            return value.Value.ToString("N0");
        }
    }
}
