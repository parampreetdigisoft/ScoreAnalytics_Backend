using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace AssessmentPlatform.Common.Implementation
{
    public partial class PdfGeneratorService : IPdfGeneratorService
    {
        #region constructor

        private readonly IAppLogger _appLogger;
        public PdfGeneratorService(IAppLogger appLogger)
        {
            _appLogger = appLogger;
        }
        #endregion


        #region pdf pillars and city report

        public async Task<byte[]> GenerateAllCitiesDetailsPdf(List<AiCitySummeryDto> cities, Dictionary<int, List<AiCityPillarReponse>> pillarsDict, List<KpiChartItem> kpis, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;
                var document = Document.Create(container =>
                {
                    foreach(var cityDetails in cities)
                    {
                        if(pillarsDict.TryGetValue(cityDetails.CityID, out var pillars) && pillars.Count > 0)
                        {
                            var kpiChartItems = kpis?
                            .Where(x => x.CityID == cityDetails.CityID)
                            .Take(109)
                            .ToList() ?? new List<KpiChartItem>();

                            AddCityDetailsPdf(container, cityDetails, pillars, kpiChartItems,new(), userRole, true);
                        }
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

        public async Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto cityDetails, List<AiCityPillarReponse> pillars, List<KpiChartItem> kpis, List<PeerCityHistoryReportDto> peerCity, UserRole userRole)
        {
            try
            {

                QuestPDF.Settings.EnableDebugging = true;
                var document = Document.Create(container =>
                {
                    AddCityDetailsPdf(container, cityDetails, pillars, kpis, peerCity, userRole);
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCityDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarReponse pillarData, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;

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

        public void AddCityDetailsPdf(IDocumentContainer container, AiCitySummeryDto cityDetails, List<AiCityPillarReponse> pillars, List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCities, UserRole userRole, bool isAllCities = false)
        {
            var kpiChartItems = kpis.Take(109).ToList(); 

            // Build pillar chart items (max 14)
            var pillarChartItems = pillars
            .Take(14)
            .Select(p => new PillarChartItem(
                p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName ?? "—",
                p.PillarName ?? "—",
                p.AIProgress))
            .ToList();

            // ── Section 1 : Global Dashboard ─────────────────────────────────
            if (!isAllCities)
                AddGlobalDashboardPage(container, cityDetails, pillarChartItems, kpis, userRole);


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
            if (pillars.Any())
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

            // ── Section 1 : Global Dashboard ─────────────────────────────────
            if (!isAllCities)
            {
                AddPeerCityComparisonSection(container, peerCities, cityDetails, userRole);
                AddPerformanceTrendsSection(container, peerCities, cityDetails, userRole);
            }

            // ── Section 4+ : Per-Pillar Detail ──────────────────────────────
            var accessiblePillars = pillars.Where(x => x.IsAccess && UserRole.CityUser == userRole || UserRole.CityUser != userRole).ToList();
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
            if (kpiChartItems.Any() || !isAllCities)
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
            List<PillarChartItem> pillars,   // already filtered to max 14
            List<KpiChartItem> kpis,      // already filtered to max 107
            UserRole userRole)
        {
            var vPillars = pillars.Where(p => p.Value.HasValue).ToList();
            //var vKpis = kpis.Where(k => k.Value.HasValue).ToList();

            doc.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, city, userRole, "City Performance Dashboard"));
                page.Content().Element(x =>
                    RenderDashboardContent(x, vPillars, kpis, city));
                PageFooter(page);
            });
        }

        void RenderDashboardContent(
            IContainer container,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis,
            AiCitySummeryDto city)
        {
            //var vKpis = kpis.Where(k => k.Value.HasValue).ToList();

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
                var topKpis = kpis
                    .Where(x =>
                        string.Equals(x.ShortName, "UDRI", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (topKpis.Any())
                    col.Item().Height(130).Element(x =>
                    DrawTopKpiBand(x, topKpis));

                col.Item().Height(100).Element(x =>
                    RenderKpiDistributionBand(x, kpis.Count, kpiGreen, kpiAmber, kpiRed));

                // ── Row 3 : KPI sorted sparkline ─────────────────────────────────
                if (kpis.Any())
                    col.Item().Height(120).Element(x =>
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
            PillarChartItem? best,
            PillarChartItem? worst)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(8)
                .Column(col =>
                {
                    col.Spacing(0);

                    col.Item().AlignCenter()
                        .Text("Overall City Score")
                        .FontSize(10).Bold().FontColor("#12352f");

                    // Donut chart
                    col.Item().Height(140).Canvas((canvas, size) =>
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

        void RenderPillarRadarCard(IContainer container, List<PillarChartItem> pillars)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor("#D8E8E2")
                .Padding(8)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text("Pillar Performance Radar")
                        .FontSize(10).Bold().FontColor("#12352f");

                    col.Item().Height(230).Canvas((canvas, size) =>
                        PaintSpiderChart(canvas, size, pillars));
                });
        }

        /// <summary>Renders a filled spider/radar chart onto an SKCanvas.</summary>
        static void PaintSpiderChart(SKCanvas canvas, Size size, List<PillarChartItem> pillars)
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
        static void DashboardStatCard(IContainer container, string value, string label, string bg, string textColor)
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
        //  KPI DASHBOARD PAGE  ·  numbered bar chart + full-name reference tables
        // ─────────────────────────────────────────────────────────────────────────────

        void KpiDashboardPage(IContainer container, List<KpiChartItem> kpis)
        {
            
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int green = kpis.Count(x => x.Value >= 70);
            int amber = kpis.Count(x => x.Value is >= 40 and < 70);
            int red = kpis.Count(x => x.Value < 40);
            float avg = (float)kpis.Average(x => x.Value ?? 0);

            // 18 bars per chart row — compact but legible
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 18)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();

            var topKpis = kpis
                .Where(x =>
                    string.Equals(x.ShortName, "UDRI", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase))
                .ToList();

            container.Padding(14).Column(col =>
            {
                col.Spacing(12);

                // ── top summary strip ─────────────────────────────────────────────
                col.Item().Height(70).Element(x =>
                    DrawKpiSummaryBand(x, total, green, amber, red, avg));

                if(topKpis.Any())
                    col.Item().Height(130).Element(x =>
                    DrawTopKpiBand(x,topKpis));

                // ── chart + reference-table sections ─────────────────────────────
                int offset = 0;
                foreach (var group in groups.Where(g => g.Any()))
                {
                    int localOffset = offset;          // capture for lambda
                    col.Item().Element(x => DrawKpiGroupSection(x, group, localOffset));
                    offset += group.Count;
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  SUMMARY BAND  ·  five stat cards in a dark-green strip
        // ─────────────────────────────────────────────────────────────────────────────

        static void DrawKpiSummaryBand(
            IContainer container,
            int total, int green, int amber, int red, float avg)
        {
            container
                .Background("#12352f")
                .Padding(10)
                .Row(row =>
                {
                    KpiStatPill(row.RelativeItem(), total.ToString(), "Total KPIs", "#4CAF50", "#4CAF5025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), green.ToString(), "Performing ≥ 70 %", "#4CAF50", "#4CAF5025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), amber.ToString(), "Developing 40–69 %", "#FFC107", "#FFC10725");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), red.ToString(), "Needs Improvement", "#EF5350", "#EF535025");
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), $"{avg:F1}%", "Average Score",
                        avg >= 70 ? "#4CAF50" : avg >= 40 ? "#FFC107" : "#EF5350",
                        "#4CAF5025");
                });
        }


        // ─────────────────────────────────────────────────────────────────────────────
        //  GROUP SECTION  ·  bar chart on top, two-column legend table below
        // ─────────────────────────────────────────────────────────────────────────────

        void DrawKpiGroupSection(IContainer container, List<KpiChartItem> group, int offset)
        {
            container
                .Border(1).BorderColor("#C5D9D0")
                .Column(col =>
                {
                    // bar chart — numbers printed below each bar
                    col.Item().Height(148).Element(x => DrawKpiBarChart(x, group, offset));

                    // hairline separator between chart and table
                    col.Item().Height(1).Background("#C5D9D0");

                    // two-column reference table
                    col.Item().Padding(6).Element(x => DrawKpiReferenceTable(x, group, offset));
                });
        }


        // ─────────────────────────────────────────────────────────────────────────────
        //  BAR CHART  ·  sequential index numbers below each bar (not cryptic codes)
        // ─────────────────────────────────────────────────────────────────────────────
        void DrawKpiBarChart(IContainer container, List<KpiChartItem> data, int offset)
        {
            container
                .Background(Colors.White)
                .Canvas((canvas, size) =>
                {
                    if (!data.Any()) return;

                    const float lp = 8f;   // left pad
                    const float rp = 8f;   // right pad
                    const float tp = 22f;  // top pad  (value labels)
                    const float bp = 26f;  // bottom pad (index labels)

                    float chartW = size.Width - lp - rp;
                    float chartH = size.Height - tp - bp;
                    int n = data.Count;
                    float barW = chartW / n;
                    float innerW = barW * 0.62f;
                    float barGap = (barW - innerW) / 2f;

                    // ── background grid lines ─────────────────────────────────────
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

                    // ── dashed 70 % performance threshold ────────────────────────
                    float y70 = tp + chartH - 0.70f * chartH;
                    using var threshPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#2E7D32").WithAlpha(100),
                        StrokeWidth = 0.9f,
                        PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0),
                        IsAntialias = true
                    };
                    canvas.DrawLine(lp, y70, lp + chartW, y70, threshPaint);

                    // ── paint reused across bars ──────────────────────────────────
                    using var valLblPaint = new SKPaint
                    { TextSize = 6.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };
                    using var numLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse("#546E7A"),
                        TextSize = 6.5f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };

                    // ── bars ──────────────────────────────────────────────────────
                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value ?? 0);
                        float bx = lp + i * barW + barGap;
                        float bh = v / 100f * chartH;
                        float by = tp + chartH - bh;
                        SKColor color = GetColor(v);
                        SKColor textcolor = v > 85 ? SKColors.White : SKColors.Black;


                        // ghost (full-height tinted background)
                        using var ghostPaint = new SKPaint
                        { Color = color.WithAlpha(35), IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, tp, bx + innerW, tp + chartH), 2, 2),
                            ghostPaint);

                        // filled bar with linear gradient
                        using var shader = SKShader.CreateLinearGradient(
                            new SKPoint(0, by), new SKPoint(0, tp + chartH),
                            new[] { color, color.WithAlpha(180) },
                            null, SKShaderTileMode.Clamp);
                        using var barPaint = new SKPaint { Shader = shader, IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, by, bx + innerW, tp + chartH), 2, 2),
                            barPaint);

                        // top cap accent line
                        using var capPaint = new SKPaint
                        {
                            Color = color,
                            StrokeWidth = 2.5f,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        canvas.DrawLine(bx + 1, by, bx + innerW - 1, by, capPaint);

                        // value label above bar
                        float vly = by - 3f;
                        if (vly < tp + 8f) vly = by + 10f;
                        valLblPaint.Color = textcolor;
                        canvas.DrawText($"{v:F1}%", bx + innerW / 2f, vly, valLblPaint);

                        // ── sequential index number below bar (e.g. "1", "2", …) ──
                        // Users cross-reference this with the legend table below.
                        canvas.DrawText(
                            $"{offset + i + 1}",
                            bx + innerW / 2f,
                            size.Height - 6f,
                            numLblPaint);
                    }
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  REFERENCE TABLE  ·  two-column layout, colored status bar, full KPI names
        // ─────────────────────────────────────────────────────────────────────────────
        void DrawKpiReferenceTable(IContainer container, List<KpiChartItem> group, int offset)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(x => DrawKpiInterpretationSection(x, group, offset));
            });
        }


        /// <summary>
        /// Renders all KPIs as paired cards — 2 per row.
        /// Each card: coloured header (code, name, score) + 5-row interpretation mini-table
        /// with the matching range row highlighted.
        /// </summary>
        void DrawKpiInterpretationSection(IContainer container, List<KpiChartItem> allItems,int offset)
        {
            // ── split into rows of 2 ─────────────────────────────────────────────
            var pairs = allItems
                .Select((item, idx) => (item, idx))
                .GroupBy(t => t.idx / 2)
                .Select(g => g.ToList())
                .ToList();

            container.Column(col =>
            {
                col.Spacing(4);

                foreach (var pair in pairs)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(4);

                        foreach (var (kpi, idx) in pair)
                            row.RelativeItem().Column(card => DrawKpiCard(card, kpi, offset + idx + 1));

                        // pad last row if odd number of KPIs
                        if (pair.Count == 1)
                            row.RelativeItem().Element(_ => { });
                    });
                }
            });
        }
        void DrawKpiCard(ColumnDescriptor card, KpiChartItem kpi, int num)
        {
            var value = kpi.Value ?? 0;
            var v = value == 100 ? Math.Round(value, 0) : Math.Round(value, 1);
            string accent = GetBarColor((float)v);

            var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
            FiveLevelInterpretationsDto? matched = interps.FirstOrDefault(x =>
                x.MinRange.HasValue && x.MaxRange.HasValue &&
                value >= x.MinRange.Value && value <= x.MaxRange.Value);

            if (matched == null && interps.Any())
                matched = interps
                    .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                    .OrderBy(x => Math.Min(
                        Math.Abs(value - x.MinRange!.Value),
                        Math.Abs(value - x.MaxRange!.Value)))
                    .FirstOrDefault();

            card.Item()
                .Border(0.5f).BorderColor(accent)
                .Column(inner =>
                {
                    // ── 1. KPI header band ──────────────────────────────────────────
                    // Definition removed from here — gets its own strip below
                    inner.Item()
                         .Background(accent)
                         .PaddingHorizontal(5).PaddingVertical(3)
                         .Row(h =>
                         {
                             // Number bubble
                             h.ConstantItem(16)
                              .AlignMiddle()
                              .Background("#00000022")
                              .AlignCenter()
                              .Text($"{num}")
                              .FontSize(6f).Bold().FontColor("#FFFFFF");

                             // Code + Name
                             h.RelativeItem()
                              .PaddingLeft(4)
                              .AlignMiddle()
                              .Column(nc =>
                              {
                                  nc.Item()
                                    .Text(kpi.ShortName ?? "")
                                    .FontSize(7.5f).Bold().FontColor("#FFFFFF");
                                  nc.Item()
                                    .Text(kpi.Name ?? "")
                                    .FontSize(5f).FontColor("#FFFFFFBB");
                              });

                             // Score
                             h.ConstantItem(34)
                              .AlignMiddle().AlignRight()
                              .Text($"{v}%")
                              .FontSize(9.5f).Bold().FontColor("#FFFFFF");
                         });

                    // ── 2. Definition strip ─────────────────────────────────────────
                    // Shown only when definition exists; wraps gracefully for long text
                    if (!string.IsNullOrWhiteSpace(kpi.Definition))
                    {
                        inner.Item()
                             .Background("#F2F6F4")                      // very pale green-grey
                             .BorderTop(0.3f).BorderColor(accent)
                             .BorderBottom(0.3f).BorderColor("#DDDDDD")
                             .PaddingHorizontal(5).PaddingVertical(3)
                             .Row(dr =>
                             {
                                 // Small label pill
                                 dr.ConstantItem(28)
                                   .AlignTop()
                                   .PaddingTop(0.5f)
                                   .Text("DEF")
                                   .FontSize(4.5f).Bold()
                                   .FontColor(accent);

                                 // Definition text — italic, wraps, keeps card compact
                                 dr.RelativeItem()
                                   .Text(kpi.Definition)
                                   .FontSize(5.5f).Italic()
                                   .FontColor("#444444")
                                   .LineHeight(1.25f);
                             });
                    }

                    // ── 3. Interpretation column sub-header ─────────────────────────
                    inner.Item()
                         .Background("#EBEBEB")
                         .PaddingHorizontal(4).PaddingVertical(2)
                         .Row(sh =>
                         {
                             sh.ConstantItem(46)
                               .Text("Range")
                               .FontSize(5.5f).Bold().FontColor("#666666");
                             sh.RelativeItem()
                               .Text("Condition")
                               .FontSize(5.5f).Bold().FontColor("#666666");
                         });

                    // ── 4. Five interpretation rows ─────────────────────────────────
                    for (int i = 0; i < interps.Count; i++)
                    {
                        var interp = interps[i];
                        bool isHit = interp == matched;

                        string rowBg = isHit ? accent : (i % 2 == 0 ? "#FFFFFF" : "#F7F7F7");
                        string rangeFg = isHit ? "#FFFFFF" : "#888888";
                        string condFg = isHit ? "#FFFFFF" : "#333333";

                        string rangeStr = (interp.MinRange.HasValue && interp.MaxRange.HasValue)
                            ? $"{Math.Round(interp.MinRange.Value, 0)}–{Math.Round(interp.MaxRange.Value, 0)}"
                            : "—";

                        inner.Item()
                             .BorderBottom(0.3f).BorderColor("#E0E0E0")
                             .Background(rowBg)
                             .PaddingHorizontal(4).PaddingVertical(2)
                             .Row(r =>
                             {
                                 r.ConstantItem(46)
                                  .Text(rangeStr)
                                  .FontSize(6f).FontColor(rangeFg);

                                 r.RelativeItem()
                                  .Text(interp.Condition ?? "—")
                                  .FontSize(6.5f)
                                  .Bold()
                                  .FontColor(condFg);
                             });
                    }
                });
        }


        // ── Top tow  kpis ────────────────────────────────

        void DrawTopKpiBand(IContainer container, List<KpiChartItem> kpis)
        {
            // ── split into rows of 2 ─────────────────────────────────────────────
            var pairs = kpis
                .Select((item, idx) => (item, idx))
                .GroupBy(t => t.idx / 2)
                .Select(g => g.ToList())
                .ToList();

            container.Column(col =>
            {
                foreach (var pair in pairs)
                {
                    col.Item().Height(130).Row(row =>
                    {
                        foreach (var (kpi, idx) in pair)
                            row.RelativeItem().Column(card => DrawKpiCard(card, kpi, idx + 1));

                        // pad last row if odd number of KPIs
                        if (pair.Count == 1)
                            row.RelativeItem().Element(_ => { });
                    });
                }
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

        // ─────────────────────────────────────────────────────────────────────────────
        //  PILLAR OVERVIEW PAGE  ·  redesigned horizontal bar layout + ring chart
        // ─────────────────────────────────────────────────────────────────────────────

        void PillarLineChartPage(IContainer container, List<PillarChartItem> pillars)
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

        static void DrawPillarHorizontalBars(IContainer container, List<PillarChartItem> data)
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
            IContainer container, float avg, PillarChartItem best, PillarChartItem worst)
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

        void DrawPillarsRadialChart(IContainer container, List<PillarChartItem> pillars)
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
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/veridian-urban-index.png");

            container.Column(column =>
            {
                column.Item().Background("#134534").Padding(12).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        string title = string.IsNullOrEmpty(pillarName) ? data.CityName : pillarName;

                        col.Item().Text(title)
                            .FontSize(21)
                            .Bold()
                            .FontColor(Colors.White);

                        col.Item().Text($"{data.CityName}, {data.State}, {data.Country} | Data Year: {data.ScoringYear}")
                            .FontSize(10)
                            .FontColor("#E8F3F0");

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor("#CFE3DD");
                    });

                    // Right logo
                    row.ConstantItem(80)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(Colors.White)
                        .Padding(4)
                        .Image(logoPath)
                        .FitArea();
                });

                // Divider
                column.Item().LineHorizontal(1).LineColor("#d9e2df");
            });
        }

        void PillarComposeHeader(IContainer container, AiCityPillarReponse data)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/veridian-urban-index.png");

            container.Column(column =>
            {
                column.Item().Background("#134534").Padding(12).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        col.Item().Text(data.PillarName)
                            .FontSize(21)
                            .Bold()
                            .FontColor(Colors.White);

                        col.Item().Text($"{data.CityName}, {data.State}, {data.Country} | Data Year: {data.AIDataYear}")
                            .FontSize(10)
                            .FontColor("#E8F3F0");

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor("#CFE3DD");
                    });

                    // Logo
                    row.ConstantItem(60)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(Colors.White)
                        .Padding(4)
                        .Image(logoPath)
                        .FitArea();
                });

                column.Item().LineHorizontal(1).LineColor("#d9e2df");
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
                    PillarContentSection(c, "Cross-Pillar System Dynamics", data.CrossPillarPatterns, "#6e9688"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Institutional Capacity Assessment", data.InstitutionalCapacity, "#0d8057"));

                //column.Item().PageBreak();
                //column.Item().PaddingTop(10).Element(c =>
                //    PillarContentSection(c, "Equity Assessment", data.EquityAssessment, "#a4bab2"));
                //column.Item().PaddingTop(10).Element(c =>
                //    PillarContentSection(c, "Sustainability Outlook", data.SustainabilityOutlook, "#373d3b"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Strategic Policy Priorities", data.StrategicRecommendations, "#2e9975"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Why This Assessment Matters", data.DataTransparencyNote, "#63a68f"));
            });
        }

        void PillarComposeContent(
            IContainer container, AiCityPillarReponse data, UserRole userRole)
        {
            container.PaddingTop(8).Column(column =>
            {
                column.Item().PaddingTop(10).Element(c => PillarProgressSection(c, data, userRole));

                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Evidence Summary", data.EvidenceSummary, "#163329"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Red Flags", data.RedFlags, "#ED561A", "#eb4634"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Geographic Equity Note", data.GeographicEquityNote, "#0d8057"));

                column.Item().PageBreak();
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Institutional Assessment", data.InstitutionalAssessment, "#2e9975"));
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Analytical Foundations and Data Integration", data.DataGapAnalysis, "#a4bab2"));

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
            IContainer container, string title, string content, string accentColor, string textcolor = "#424242")
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
                    .FontSize(10).LineHeight(1.6f).FontColor(textcolor);
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
            if (value >= 70) return "#2E7D32";
            if (value >= 40) return "#F9A825";
            return "#C62828";
        }

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

    public partial class PdfGeneratorService
    {
        // ══════════════════════════════════════════════════════════════════════════
        //  CONSTANTS  – tweak here to adjust chart sizing for 4-6 peers / 14 pillars
        // ══════════════════════════════════════════════════════════════════════════

        private const int MaxPillars = 14;

        // Palette: index 0 = selected city (gold), 1-5 = peer cities
        private static readonly string[] CityPalette =
        {
            "#F0B429",   // gold  – selected city
            "#4CAF8A",   // teal
            "#1E88E5",   // blue
            "#FB8C00",   // orange
            "#7B61FF",   // purple
            "#E05252",   // red
        };

        // Pillar palette (up to 14 distinct colours)
        private static readonly string[] PillarPalette =
        {
            "#12352F","#336B58","#4CAF8A","#F0B429","#F5A623",
            "#E05252","#7B61FF","#1E88E5","#43A047","#FB8C00",
            "#0097A7","#8D6E63","#E91E63","#607D8B"
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS  – called from AddCityDetailsPdf
        // ══════════════════════════════════════════════════════════════════════════

        void AddPeerCityComparisonSection(
            IDocumentContainer container,
            List<PeerCityHistoryReportDto> peerCities,
            AiCitySummeryDto cityDetails,
            UserRole userRole)
        {
            if (peerCities == null || !peerCities.Any()) return;

            // Separate: main city entry + actual peer entries (cap at MaxPeerCities)
            var main = FindMainCity(peerCities, cityDetails);
            var peers = peerCities
                .Where(p => !IsSameCity(p.CityName, cityDetails.CityName))
                .ToList();

            // ── 5.1  Population-Based ────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Population-Based Peer Comparison"));
                page.Content().Element(c =>
                    PopulationPeerPage(c, peers, main, cityDetails));
                PageFooter(page);
            });

            // ── 5.2  Regional ────────────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Regional Peer Group Comparison"));
                page.Content().Element(c =>
                    RegionalPeerPage(c, peers, main, cityDetails));
                PageFooter(page);
            });

            // ── 5.3  Income-Level ────────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Income-Level Peer Comparison"));
                page.Content().Element(c =>
                    IncomePeerPage(c, peers, main, cityDetails));
                PageFooter(page);
            });

           
            // ── 5.5  Relative Ranking ────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Relative Ranking Among Peer Cities"));
                page.Content().Element(c =>
                    RelativeRankingPage(c, peers, main, cityDetails));
                PageFooter(page);
            });
        }

        void AddPerformanceTrendsSection(
            IDocumentContainer container,
            List<PeerCityHistoryReportDto> peerCities,
            AiCitySummeryDto cityDetails,
            UserRole userRole)
        {
            if (peerCities == null || !peerCities.Any()) return;

            var main = FindMainCity(peerCities, cityDetails);
            var peers = peerCities
                .Where(p => !IsSameCity(p.CityName, cityDetails.CityName))      
                .ToList();

            // ── 6.1 + 6.2  Historical & Five-Year Evolution ──────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Performance Trends Over Time"));
                page.Content().Element(c =>
                    HistoricalTrendsPage(c, peers, main, cityDetails));
                PageFooter(page);
            });

            // ── 6.3  Pillar-Level Trend ──────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CityComposeHeader(x, cityDetails, userRole, "Pillar-Level Trend Analysis"));
                page.Content().Element(c =>
                    PillarTrendPage(c, main, cityDetails));
                PageFooter(page);
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.1  POPULATION-BASED PEER GROUP COMPARISON
        // ══════════════════════════════════════════════════════════════════════════

        void PopulationPeerPage(
            IContainer container,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main,
            AiCitySummeryDto cityDetails)
        {
            // All cities (main + peers) sorted by population desc
            var all = BuildAllCities(main, peers)
                .Where(c => c.Population.HasValue)
                .OrderByDescending(c => c.Population)
                .ToList();

            if (!all.Any()) { DrawNoDataPage(container); return; }

            long maxPop = (long)(all.Max(p => p.Population) ?? 1);

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{all.Count} cities compared  |  " +
                    $"Largest: {all.First().CityName} ({FormatPop(all.First().Population)})  |  " +
                    $"Smallest: {all.Last().CityName} ({FormatPop(all.Last().Population)})"));

                // ── Population bar chart ──────────────────────────────────────
                col.Item().Text("Population Size by City")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(all.Count * 40).Canvas((canvas, size) =>
                    DrawPopulationBars(canvas, size, all, cityDetails, maxPop));

                col.Item().Element(x => DrawCityLegend(x, all, cityDetails));

                // ── Score vs Population scatter ───────────────────────────────
                col.Item().PaddingTop(8)
                    .Text("Score vs Population  (each dot = one city)")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(all.Count * 40).Canvas((canvas, size) =>
                    DrawScatterPlot(canvas, size, all, cityDetails,
                        c => (float)(c.Population ?? 0),
                        c => GetLatestScoreOrZero(c),
                        "Population", "Score"));
            });
        }

        void DrawPopulationBars(
            SKCanvas canvas, Size size,
            List<PeerCityHistoryReportDto> cities,
            AiCitySummeryDto cityDetails,
            long maxPop)
        {
            float rowH = 30f;
            float labelW = 130f;
            float barArea = size.Width - labelW - 72f;

            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                float y = i * rowH + 4f;
                float barW = (float)((city.Population ?? 0) / (double)maxPop * barArea);
                bool isMain = IsSameCity(city.CityName, cityDetails.CityName);

                // Row background
                if (i % 2 == 0)
                    canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + rowH - 4),
                        new SKPaint { Color = SKColor.Parse("#f4f7f5") });

                // Highlight selected city row
                if (isMain)
                    canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + rowH - 4),
                        new SKPaint { Color = SKColor.Parse("#FFF8E1") });

                DrawCanvasText(canvas, city.CityName, 4, y + 5, 9,
                    isMain ? "#12352f" : "#444444", bold: isMain);

                string barColor = isMain ? CityPalette[0] : CityPalette[1 + (i % (CityPalette.Length - 1))];
                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(labelW, y + 4, labelW + barW, y + rowH - 6), 3),
                    new SKPaint { Color = SKColor.Parse(barColor), IsAntialias = true });

                DrawCanvasText(canvas, FormatPop(city.Population),
                    labelW + barW + 5, y + 5, 9, "#555555");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.2  REGIONAL PEER GROUP COMPARISON
        // ══════════════════════════════════════════════════════════════════════════

        void RegionalPeerPage(
            IContainer container,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main,
            AiCitySummeryDto cityDetails)
        {
            var all = BuildAllCities(main, peers);

            var byRegion = all
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Region) ? p.Country ?? "Unknown" : p.Region)
                .OrderByDescending(g => g.Count())
                .ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{byRegion.Count} region(s)  |  {all.Count} total cities analysed"));

                col.Item().Text("City Distribution by Region")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(180).Canvas((canvas, size) =>
                    DrawDonutChart(canvas, size,
                        byRegion.Select(g => (g.Key, (float)g.Count())).ToList()));

                col.Item().PaddingTop(4)
                    .Text("Average Score per Region")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Canvas((canvas, size) =>
                {
                    var regionScores = byRegion
                        .Select(g => (
                            Region: g.Key,
                            Avg: g.Average(c => GetLatestScoreOrZero(c)),  // includes 0
                            Count: g.Count()
                        ))
                        .OrderByDescending(r => r.Avg)
                        .ToList();

                    float barH = 28f;
                    float labelW = 120f;
                    float barArea = size.Width - labelW - 65f;

                    for (int i = 0; i < regionScores.Count; i++)
                    {
                        var r = regionScores[i];
                        float y = i * barH + 4f;
                        float barW = r.Avg / 100f * barArea;

                        if (i % 2 == 0)
                            canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + barH - 4),
                                new SKPaint { Color = SKColor.Parse("#f4f7f5") });

                        DrawCanvasText(canvas, r.Region, 4, y + 5, 9, "#333333");
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(labelW, y + 3, labelW + barW, y + barH - 6), 3),
                            new SKPaint { Color = SKColor.Parse(ScoreColor(r.Avg)), IsAntialias = true });
                        DrawCanvasText(canvas, $"{r.Avg:F1}  (n={r.Count})",
                            labelW + barW + 5, y + 5, 9, "#555555");
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.3  INCOME-LEVEL PEER COMPARISON
        // ══════════════════════════════════════════════════════════════════════════

        void IncomePeerPage(
            IContainer container,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main,
            AiCitySummeryDto cityDetails)
        {
            var all = BuildAllCities(main, peers);
            var withIncome = all.Where(p => p.Income.HasValue).OrderBy(p => p.Income).ToList();

            if (!withIncome.Any()) { DrawNoDataPage(container); return; }

            int q = Math.Max(1, withIncome.Count / 4);
            var segments = new[]
            {
                ("Low Income",    withIncome.Take(q).ToList()),
                ("Lower-Middle",  withIncome.Skip(q).Take(q).ToList()),
                ("Upper-Middle",  withIncome.Skip(2 * q).Take(q).ToList()),
                ("High Income",   withIncome.Skip(3 * q).ToList())
            };
            string[] segColors = { "#E05252", "#F5A623", "#4CAF8A", "#12352F" };

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"Income quartile analysis  |  {withIncome.Count} cities  |  " +
                    $"Range: {withIncome.Min(p => p.Income):C0} – {withIncome.Max(p => p.Income):C0}"));

                // ── Avg score per quartile bars ──────────────────────────────
                col.Item().Text("Average Score by Income Quartile")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(130).Canvas((canvas, size) =>
                {
                    float barAreaW = (size.Width - 40f) / 4f - 8f;
                    for (int i = 0; i < segments.Length; i++)
                    {
                        var (label, cities) = segments[i];
                        if (!cities.Any()) continue;

                        // count 0 scores: use GetLatestScoreOrZero
                        float avg = cities.Average(c => GetLatestScoreOrZero(c));
                        float barH = avg / 100f * 90f;
                        float x = 20 + i * ((size.Width - 40f) / 4f);

                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(x, 100 - barH, x + barAreaW, 100), 4),
                            new SKPaint { Color = SKColor.Parse(segColors[i]), IsAntialias = true });

                        DrawCanvasText(canvas, $"{avg:F1}", x + barAreaW / 2 - 10, 100 - barH - 14, 9,
                            "#12352f", bold: true);
                        DrawCanvasText(canvas, label, x, 108, 8, "#555555");
                        DrawCanvasText(canvas, $"n={cities.Count}", x, 118, 8, "#888888");
                    }
                });

                // ── Scatter: Income vs Score ──────────────────────────────────
                col.Item().PaddingTop(4)
                    .Text("Income vs Composite Score  (each dot = one city)")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(160).Canvas((canvas, size) =>
                    DrawScatterPlot(canvas, size, withIncome, cityDetails,
                        c => (float)(c.Income ?? 0),
                        c => GetLatestScoreOrZero(c),
                        "Income (USD)", "Score"));

                // ── Top performers table ──────────────────────────────────────
                col.Item().PaddingTop(6)
                    .Text("Top Performers by Income Group")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(100);
                        cols.ConstantColumn(60);
                        cols.ConstantColumn(100);
                        cols.RelativeColumn();
                        cols.ConstantColumn(60);
                    });
                    DrawTableHeader(table, new[] {  "City", "Country", "Score", "Income Group", "Income", });

                    foreach (var (label, cities) in segments)
                    {
                        foreach (var city in cities.OrderByDescending(c => GetLatestScoreOrZero(c)).Take(2))
                        {
                            bool isMain = IsSameCity(city.CityName, cityDetails.CityName);
                            string rowBg = isMain ? "#fff9e6" : Colors.White;
                            float score = GetLatestScoreOrZero(city);
                            var income = FormatPop(city.Income);
       
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(5).Text(city.CityName).FontSize(8)
                                .FontColor(isMain ? "#12352f" : "#333333");
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(5).Text(city.Country ?? "—").FontSize(8).FontColor("#555555");
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(5)
                                .Text($"{score:F1}").FontSize(8).Bold().FontColor(ScoreColor(score));
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(5).Text(label).FontSize(8).FontColor("#555555");
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(5).Text(income.ToString()).FontSize(8).FontColor("#555555");
                        }
                    }
                });
            });
        }

        
        // ══════════════════════════════════════════════════════════════════════════
        //  5.5  RELATIVE RANKING AMONG PEER CITIES
        // ══════════════════════════════════════════════════════════════════════════

        void RelativeRankingPage(
            IContainer container,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main,
            AiCitySummeryDto cityDetails)
        {
            // Build ranked list including main city; include 0-score cities
            var all = BuildAllCities(main, peers)
                .Select(c => (City: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int total = all.Count;
            int mainRank = all.FindIndex(r => IsSameCity(r.City.CityName, cityDetails.CityName)) + 1;
            float mainScore = mainRank > 0 ? all[mainRank - 1].Score : 0f;
            float pctile = mainRank > 0 ? (1f - (float)mainRank / total) * 100f : 0f;

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                // ── Hero rank banner ──────────────────────────────────────────
                col.Item().Background("#12352f").Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"#{mainRank} of {total}")
                            .FontSize(32).Bold().FontColor("#f0b429");
                        c.Item().Text($"{cityDetails.CityName}  \u00b7  {cityDetails.Country}")
                            .FontSize(12).FontColor("#a5d6c2");
                    });
                    row.ConstantItem(130).Column(c =>
                    {
                        c.Item().AlignRight().Text("Score").FontSize(9).FontColor("#a5a8ad");
                        c.Item().AlignRight().Text($"{mainScore:F1}")
                            .FontSize(28).Bold().FontColor(Colors.White);
                        c.Item().AlignRight().Text($"Top {100 - pctile:F0}% of peers")
                            .FontSize(10).FontColor("#4caf8a");
                    });
                });

                // ── Score distribution histogram ──────────────────────────────
                col.Item().Text("Score Distribution Among All Cities")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(150).Canvas((canvas, size) =>
                    DrawHistogram(canvas, size,
                        all.Select(r => r.Score).ToList(), mainScore, 10));

                // ── Full ranking table ────────────────────────────────────────
                col.Item().Text("Full City Ranking").FontSize(11).Bold().FontColor("#12352f");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(24);   // rank
                        cols.RelativeColumn();     // city name
                        cols.ConstantColumn(65);   // country
                        cols.ConstantColumn(55);   // region
                        cols.ConstantColumn(52);   // population
                        cols.ConstantColumn(70);   // score bar
                    });

                    DrawTableHeader(table, new[] { "#", "City", "Country", "Region", "Pop.", "Score" });


                    foreach (var (entry, idx) in all.Select((e, i) => (e, i)))
                    {
                        bool isMain = IsSameCity(entry.City.CityName, cityDetails.CityName);
                        string bg = isMain ? "#fff9e6" : (idx % 2 == 0 ? Colors.White : "#fafafa");
                        string rankColor = idx == 0 ? "#f0b429"
                                         : idx == 1 ? "#a5a8ad"
                                         : idx == 2 ? "#cd7f32" : "#555555";

                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e8e8e8")
                            .Padding(4).Text($"{idx + 1}").FontSize(8).FontColor(rankColor);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e8e8e8")
                            .Padding(4).Text(entry.City.CityName).FontSize(8)
                            .FontColor(isMain ? "#12352f" : "#333333");
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e8e8e8")
                            .Padding(4).Text(entry.City.Country ?? "—").FontSize(8).FontColor("#555555");
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e8e8e8")
                            .Padding(4).Text(entry.City.Region ?? "—").FontSize(8).FontColor("#555555");
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e8e8e8")
                            .Padding(4).AlignRight()
                            .Text(FormatPop(entry.City.Population)).FontSize(8).FontColor("#555555");

                        table.Cell()
                        .Background(bg)
                        .BorderBottom(0.5f)
                        .BorderColor("#e8e8e8")
                        .Padding(4)
                        .Row(r =>
                        {
                            var percent = entry.Score / 100f;

                            r.RelativeItem().Height(10).Background("#eeeeee").Layers(layer =>
                            {
                                layer.PrimaryLayer().Background("#eeeeee");

                                layer.Layer().Width((float)percent * 100)
                                    .Background(ScoreColor(entry.Score));
                            });

                            r.ConstantItem(24).AlignRight().Text($"{entry.Score:F1}")
                                .FontSize(8)
                                .FontColor("#333333");
                        });
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  6.1 + 6.2  HISTORICAL PERFORMANCE TRENDS
        // ══════════════════════════════════════════════════════════════════════════

        void HistoricalTrendsPage(
            IContainer container,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? main,
            AiCitySummeryDto cityDetails)
        {
            var all = BuildAllCities(main, peers);
            var mainCity = main ?? peers.FirstOrDefault();

            if (mainCity == null) { DrawNoDataPage(container); return; }

            // All distinct years across all cities
            var allYears = all
                .SelectMany(c => c.CityHistory ?? Enumerable.Empty<PeerCityYearHistoryDto>())
                .Select(h => h.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            if (!allYears.Any()) { DrawNoDataPage(container); return; }

            var mainHistory = (mainCity.CityHistory ?? new())
                .OrderBy(h => h.Year).ToList();

            // Peer average per year (include 0-score years)
            var peerAvg = allYears.Select(yr =>
            {
                var scores = peers
                    .Select(p => p.CityHistory?.FirstOrDefault(h => h.Year == yr))
                    .Select(h => h != null ? (float?)h.ScoreProgress : null)
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .ToList();
                return (Year: yr, Avg: scores.Any() ? scores.Average() : 0f, HasData: scores.Any());
            }).ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(14);

                // Insight
                if (mainHistory.Count >= 2)
                {
                    float first = (float)mainHistory.First().ScoreProgress;
                    float last = (float)mainHistory.Last().ScoreProgress;
                    float delta = last - first;
                    col.Item().Element(x => DrawInsightBand(x,
                        $"Period: {allYears.First()} – {allYears.Last()}  |  " +
                        $"{mainCity.CityName}: {(delta >= 0 ? "+" : "")}{delta:F1} pts  |  " +
                        $"Latest score: {last:F1}  |  " +
                        $"{peers.Count} peer city(ies)"));
                }

                // ── 6.1  Multi-line trend ────────────────────────────────────
                col.Item().Text("6.1  Historical Score Trend")
                    .FontSize(12).Bold().FontColor("#12352f");

                col.Item().Height(190).Canvas((canvas, size) =>
                    DrawMultiLineTrendChart(canvas, size, allYears, peers, mainCity, cityDetails, peerAvg));

                // Legend: one entry per city
                col.Item().Element(x => DrawCityLineLegend(x, mainCity, peers, cityDetails));

                col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor("#e0e0e0");

                // ── 6.2  Five-year area chart ────────────────────────────────
                col.Item().Text("6.2  Five-Year Composite Score Evolution")
                    .FontSize(12).Bold().FontColor("#12352f");

                var last5 = allYears.TakeLast(5).ToList();
                var mainLast5 = mainHistory.Where(h => last5.Contains(h.Year))
                                            .OrderBy(h => h.Year).ToList();
                var peerLast5 = peerAvg.Where(p => last5.Contains(p.Year))
                                        .OrderBy(p => p.Year).ToList();

                col.Item().Height(120).Canvas((canvas, size) =>
                    DrawAreaComparisonChart(canvas, size, last5, mainLast5,
                        peerLast5.Select(p => (p.Year, p.Avg)).ToList()));

                // YoY table
                if (mainLast5.Count > 1)
                    col.Item().Element(x =>
                        DrawYoYTable(x, last5, mainLast5,
                            peerLast5.Select(p => (p.Year, p.Avg)).ToList()));
            });
        }

        void DrawYoYTable(
            IContainer container,
            List<int> years,
            List<PeerCityYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(65);
                    for (int i = 0; i < years.Count; i++) cols.RelativeColumn();
                });

                // Header
                table.Cell().Background("#12352f").Padding(5)
                    .Text("Metric").FontSize(8).Bold().FontColor(Colors.White);
                foreach (var yr in years)
                    table.Cell().Background("#12352f").Padding(5).AlignRight()
                        .Text(yr.ToString()).FontSize(8).Bold().FontColor(Colors.White);

                // Score row
                table.Cell().Background("#f4f7f5").Padding(5)
                    .Text("Score").FontSize(8).FontColor("#333333");
                foreach (var yr in years)
                {
                    float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    table.Cell().Background("#f4f7f5").Padding(5).AlignRight()
                        .Text($"{s:F1}").FontSize(8).Bold().FontColor(ScoreColor(s));
                }

                // YoY delta
                table.Cell().Background(Colors.White).Padding(5)
                    .Text("YoY \u0394").FontSize(8).FontColor("#333333");
                for (int i = 0; i < years.Count; i++)
                {
                    if (i == 0)
                    {
                        table.Cell().Background(Colors.White).Padding(5)
                            .Text("—").FontSize(8).FontColor("#aaaaaa");
                        continue;
                    }
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[i - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == years[i])?.ScoreProgress ?? 0);
                    float d = curr - prev;
                    table.Cell().Background(Colors.White).Padding(5).AlignRight()
                        .Text($"{(d >= 0 ? "+" : "")}{d:F1}").FontSize(8)
                        .FontColor(d >= 0 ? "#336b58" : "#e05252");
                }

                // vs Peers
                table.Cell().Background("#f4f7f5").Padding(5)
                    .Text("vs Peers").FontSize(8).FontColor("#333333");
                foreach (var yr in years)
                {
                    float myS = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float pAvg = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    float d = myS - pAvg;
                    table.Cell().Background("#f4f7f5").Padding(5).AlignRight()
                        .Text($"{(d >= 0 ? "+" : "")}{d:F1}").FontSize(8)
                        .FontColor(d >= 0 ? "#336b58" : "#e05252");
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  6.3  PILLAR-LEVEL TREND  (main city only; up to 14 pillars)
        // ══════════════════════════════════════════════════════════════════════════

        void PillarTrendPage(
            IContainer container,
            PeerCityHistoryReportDto? mainCity,
            AiCitySummeryDto cityDetails)
        {
            if (mainCity == null) { DrawNoDataPage(container); return; }

            var history = mainCity.CityHistory ?? new();
            var allYears = history.Select(h => h.Year).OrderBy(y => y).ToList();

            if (!allYears.Any()) { DrawNoDataPage(container); return; }

            // Collect all unique pillars (cap at MaxPillars = 14)
            var pillars = history
                .SelectMany(h => h.Pillars ?? Enumerable.Empty<PeerCityPillarHistoryReportDto>())
                .GroupBy(p => p.PillarID)
                .Select(g => g.OrderBy(p => p.DisplayOrder).First())
                .OrderBy(p => p.DisplayOrder)
                .Take(MaxPillars)
                .ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{pillars.Count} pillar(s)  |  {allYears.Count} year(s)  |  City: {mainCity.CityName}"));

                col.Item().Text("Pillar Score Trajectory Over Time")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Height(200).Canvas((canvas, size) =>
                    DrawPillarLineChart(canvas, size, allYears, history, pillars));

                //// Pillar colour legend
                col.Item().Element(x => DrawLegend(x,
                    pillars.Select((p, i) =>
                        (PillarPalette[i % PillarPalette.Length], p.PillarName)).ToArray(), 10));

                // ── Pillar heatmap table ──────────────────────────────────────
                col.Item().PaddingTop(4)
                    .Text("Pillar Score Heatmap  (darker = higher score)")
                    .FontSize(11).Bold().FontColor("#12352f");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(110);
                        foreach (var _ in allYears) cols.RelativeColumn();
                    });

                    table.Cell().Background("#12352f").Padding(5)
                        .Text("Pillar").FontSize(8).Bold().FontColor(Colors.White);
                    foreach (var yr in allYears)
                        table.Cell().Background("#12352f").Padding(5).AlignCenter()
                            .Text(yr.ToString()).FontSize(8).Bold().FontColor(Colors.White);

                    foreach (var (pillar, pi) in pillars.Select((p, i) => (p, i)))
                    {
                        string rowBg = pi % 2 == 0 ? "#f4f7f5" : Colors.White;

                        table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                            .Padding(5).Text(pillar.PillarName).FontSize(8)
                            .Bold().FontColor("#12352f");

                        foreach (var yr in allYears)
                        {
                            var h = history.FirstOrDefault(h2 => h2.Year == yr);
                            var ps = h?.Pillars?.FirstOrDefault(p2 => p2.PillarID == pillar.PillarID);
                            // treat null as "no data"; 0 is a valid score
                            bool hasData = ps != null;
                            float score = hasData ? (float)ps!.ScoreProgress : -1f;

                            string cellBg = !hasData ? "#f0f0f0"
                                : InterpolateColor("#ffffff", "#12352f", score / 100f);

                            table.Cell().Background(cellBg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                                .Padding(4).AlignCenter()
                                .Text(!hasData ? "—" : $"{score:F1}").FontSize(8)
                                .FontColor(score >= 50 ? Colors.White : "#333333");
                        }
                    }
                });

                // ── Trend highlights ──────────────────────────────────────────
                if (allYears.Count >= 2 && pillars.Any())
                {
                    col.Item().PaddingTop(6)
                        .Text("Pillar Trend Highlights").FontSize(11).Bold().FontColor("#12352f");

                    var pillarDeltas = pillars.Select(p =>
                    {
                        var firstH = history.FirstOrDefault()?.Pillars?
                            .FirstOrDefault(pp => pp.PillarID == p.PillarID);
                        var lastH = history.LastOrDefault()?.Pillars?
                            .FirstOrDefault(pp => pp.PillarID == p.PillarID);
                        float d = (float)((lastH?.ScoreProgress ?? 0) - (firstH?.ScoreProgress ?? 0));
                        return (Name: p.PillarName, Delta: d);
                    }).OrderByDescending(x => x.Delta).ToList();

                    col.Item().Row(row =>
                    {
                        // Most improved  (replaced emoji with ASCII arrow)
                        row.RelativeItem().Background("#e8f5e9").Padding(10).Column(c =>
                        {
                            c.Item().Text("(+) Most Improved").FontSize(9).Bold().FontColor("#336b58");
                            foreach (var pd in pillarDeltas.Take(3))
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(Shorten(pd.Name, 28)).FontSize(8).FontColor("#333333");
                                    r.ConstantItem(44).AlignRight()
                                        .Text($"+{pd.Delta:F1}").FontSize(8).Bold().FontColor("#336b58");
                                });
                        });

                        row.ConstantItem(10);

                        // Needs attention  (replaced emoji with ASCII)
                        row.RelativeItem().Background("#fdecea").Padding(10).Column(c =>
                        {
                            c.Item().Text("(!) Needs Attention").FontSize(9).Bold().FontColor("#e05252");
                            foreach (var pd in pillarDeltas.TakeLast(3).Reverse())
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(Shorten(pd.Name, 28)).FontSize(8).FontColor("#333333");
                                    r.ConstantItem(44).AlignRight()
                                        .Text($"{pd.Delta:F1}").FontSize(8).Bold().FontColor("#e05252");
                                });
                        });
                    });
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CANVAS CHART RENDERERS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Multi-line trend chart: one coloured line per city (gold = main, palette = peers).
        /// Thin grey peer lines are no longer used; each peer gets its own distinct colour.
        /// </summary>
        void DrawMultiLineTrendChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto mainCity,
            AiCitySummeryDto cityDetails,
            List<(int Year, float Avg, bool HasData)> peerAvg)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 12f, padT = 10f, padB = 24f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            // Grid
            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse("#e8e8e8"), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, "#aaaaaa");
            }
            foreach (int yr in years)
            {
                float x = Xp(yr);
                canvas.DrawLine(x, padT, x, padT + h,
                    new SKPaint { Color = SKColor.Parse("#f0f0f0"), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, yr.ToString(), x - 14, padT + h + 7, 7, "#888888");
            }

            // Peer average (dashed green)
            var avgPts = peerAvg.Where(p => p.HasData)
                .Select(p => new SKPoint(Xp(p.Year), Yp(p.Avg))).ToList();
            DrawDashedPolyline(canvas, avgPts,
                new SKPaint
                {
                    Color = SKColor.Parse("#4CAF8A"),
                    StrokeWidth = 1.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            // Individual peer lines
            for (int pi = 0; pi < peers.Count; pi++)
            {
                var peer = peers[pi];
                string clr = CityPalette[1 + (pi % (CityPalette.Length - 1))];

                var pts = (peer.CityHistory ?? new())
                    .Where(h => years.Contains(h.Year))
                    .OrderBy(h => h.Year)
                    .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress)))
                    .ToList();

                DrawPolyline(canvas, pts,
                    new SKPaint
                    {
                        Color = SKColor.Parse(clr).WithAlpha(180),
                        StrokeWidth = 1.2f,
                        IsAntialias = true,
                        IsStroke = true
                    });
            }

            // Main city line (gold, bold)
            var mainPts = (mainCity.CityHistory ?? new())
                .Where(h => years.Contains(h.Year))
                .OrderBy(h => h.Year)
                .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress)))
                .ToList();

            DrawPolyline(canvas, mainPts,
                new SKPaint
                {
                    Color = SKColor.Parse(CityPalette[0]),
                    StrokeWidth = 2.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            foreach (var pt in mainPts)
                canvas.DrawCircle(pt.X, pt.Y, 4f,
                    new SKPaint { Color = SKColor.Parse(CityPalette[0]), IsAntialias = true });
        }

        void DrawAreaComparisonChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCityYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 10f, padT = 6f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse("#e8e8e8"), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, "#aaaaaa");
            }
            foreach (int yr in years)
                DrawCanvasText(canvas, yr.ToString(), Xp(yr) - 12, padT + h + 5, 7, "#888888");

            // Peer area
            var peerPath = new SKPath();
            peerPath.MoveTo(Xp(years.First()), Yp(0));
            foreach (int yr in years)
            {
                float v = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                peerPath.LineTo(Xp(yr), Yp(v));
            }
            peerPath.LineTo(Xp(years.Last()), Yp(0));
            peerPath.Close();
            canvas.DrawPath(peerPath,
                new SKPaint { Color = SKColor.Parse("#4CAF8A").WithAlpha(40), IsAntialias = true });

            // Main area
            var mainPath = new SKPath();
            mainPath.MoveTo(Xp(years.First()), Yp(0));
            foreach (int yr in years)
            {
                float v = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                mainPath.LineTo(Xp(yr), Yp(v));
            }
            mainPath.LineTo(Xp(years.Last()), Yp(0));
            mainPath.Close();
            canvas.DrawPath(mainPath,
                new SKPaint { Color = SKColor.Parse(CityPalette[0]).WithAlpha(50), IsAntialias = true });

            // Outlines
            DrawPolyline(canvas,
                years.Select(yr => new SKPoint(Xp(yr),
                    Yp(peerAvg.FirstOrDefault(p => p.Year == yr).Avg))).ToList(),
                new SKPaint
                {
                    Color = SKColor.Parse("#4CAF8A"),
                    StrokeWidth = 1.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            DrawPolyline(canvas,
                years.Select(yr => new SKPoint(Xp(yr),
                    Yp((float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0)))).ToList(),
                new SKPaint
                {
                    Color = SKColor.Parse(CityPalette[0]),
                    StrokeWidth = 2f,
                    IsAntialias = true,
                    IsStroke = true
                });
        }

        /// <summary>Multi-pillar line chart — one coloured line per pillar (up to 14).</summary>
        void DrawPillarLineChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCityYearHistoryDto> history,
            List<PeerCityPillarHistoryReportDto> pillars)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 10f, padT = 8f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse("#e8e8e8"), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, "#aaaaaa");
            }
            foreach (int yr in years)
                DrawCanvasText(canvas, yr.ToString(), Xp(yr) - 12, padT + h + 5, 7, "#888888");

            for (int pi = 0; pi < pillars.Count; pi++)
            {
                var pillar = pillars[pi];
                string color = PillarPalette[pi % PillarPalette.Length];

                var pts = years
                    .Select(yr =>
                    {
                        var hEntry = history.FirstOrDefault(h => h.Year == yr);
                        var ps = hEntry?.Pillars?.FirstOrDefault(p => p.PillarID == pillar.PillarID);
                        return ps != null ? (float?)ps.ScoreProgress : null;
                    })
                    .Select((s, i) => (Year: years[i], Score: s))
                    .Where(p => p.Score.HasValue)
                    .Select(p => new SKPoint(Xp(p.Year), Yp(p.Score!.Value)))
                    .ToList();

                DrawPolyline(canvas, pts,
                    new SKPaint
                    {
                        Color = SKColor.Parse(color),
                        StrokeWidth = 1.5f,
                        IsAntialias = true,
                        IsStroke = true
                    });

                foreach (var pt in pts)
                    canvas.DrawCircle(pt.X, pt.Y, 2.5f,
                        new SKPaint { Color = SKColor.Parse(color), IsAntialias = true });
            }
        }

        void DrawScatterPlot(
            SKCanvas canvas, Size size,
            List<PeerCityHistoryReportDto> cities,
            AiCitySummeryDto cityDetails,
            Func<PeerCityHistoryReportDto, float> xVal,
            Func<PeerCityHistoryReportDto, float> yVal,
            string xLabel, string yLabel)
        {
            const float padL = 42f, padR = 14f, padT = 8f, padB = 24f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float xMin = cities.Any() ? cities.Min(xVal) : 0f;
            float xMax = cities.Any() ? cities.Max(xVal) : 1f;
            if (xMax <= xMin) xMax = xMin + 1;

            float Xp(float v) => padL + (v - xMin) / (xMax - xMin) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            // Axes
            canvas.DrawLine(padL, padT, padL, padT + h,
                new SKPaint { Color = SKColor.Parse("#aaaaaa"), StrokeWidth = 0.8f });
            canvas.DrawLine(padL, padT + h, padL + w, padT + h,
                new SKPaint { Color = SKColor.Parse("#aaaaaa"), StrokeWidth = 0.8f });

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse("#eeeeee"), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, "#999999");
            }

            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                bool isMain = IsSameCity(city.CityName, cityDetails.CityName);
                float x = Xp(xVal(city));
                float y = Yp(yVal(city));
                string clr = isMain ? CityPalette[0] : CityPalette[1 + (i % (CityPalette.Length - 1))];

                canvas.DrawCircle(x, y, isMain ? 6f : 4.5f,
                    new SKPaint { Color = SKColor.Parse(clr), IsAntialias = true });

                if (isMain)
                    canvas.DrawCircle(x, y, 6f,
                        new SKPaint
                        {
                            Color = SKColor.Parse("#12352f"),
                            IsStroke = true,
                            StrokeWidth = 1.5f,
                            IsAntialias = true
                        });
            }

            DrawCanvasText(canvas, xLabel, padL + w / 2 - 22, padT + h + 14, 8, "#666666");
            DrawCanvasText(canvas, yLabel, 2, padT + h / 2, 8, "#666666");
        }

        void DrawDonutChart(SKCanvas canvas, Size size, List<(string Label, float Value)> segments)
        {
            float total = segments.Sum(s => s.Value);
            if (total <= 0) return;

            float cx = size.Height / 2f;
            float cy = size.Height / 2f;
            float outerR = size.Height / 2f - 6f;
            float innerR = outerR * 0.52f;

            float startAngle = -90f;
            for (int i = 0; i < segments.Count; i++)
            {
                float sweep = segments[i].Value / total * 360f;
                using var path = new SKPath();
                var outer = new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR);
                path.AddArc(outer, startAngle, sweep);
                path.ArcTo(new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR),
                    startAngle + sweep, -sweep, false);
                path.Close();

                canvas.DrawPath(path, new SKPaint
                { Color = SKColor.Parse(PillarPalette[i % PillarPalette.Length]), IsAntialias = true });

                startAngle += sweep;
            }

            // Centre count
            DrawCanvasText(canvas, $"{segments.Count}", cx - 8, cy - 8, 14, "#12352f", bold: true);
            DrawCanvasText(canvas, "groups", cx - 16, cy + 6, 7, "#555555");

            // Legend right of donut
            float lx = cx + outerR + 14f;
            for (int i = 0; i < segments.Count; i++)
            {
                float ly = 12 + i * 17f;
                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(lx, ly, lx + 10, ly + 10), 2),
                    new SKPaint
                    {
                        Color = SKColor.Parse(PillarPalette[i % PillarPalette.Length]),
                        IsAntialias = true
                    });
                DrawCanvasText(canvas,
                    $"{Shorten(segments[i].Label, 18)}  ({segments[i].Value:F0})",
                    lx + 14, ly, 8, "#333333");
            }
        }

        void DrawHistogram(
            SKCanvas canvas, Size size,
            List<float> scores, float markerValue, int bins)
        {
            if (!scores.Any()) return;

            const float padL = 30f, padR = 10f, padT = 6f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;
            float binW = w / bins;
            float bucketSz = 100f / bins;

            int[] counts = new int[bins];
            foreach (float s in scores)
            {
                int b = Math.Clamp((int)(s / bucketSz), 0, bins - 1);
                counts[b]++;
            }
            int maxCount = counts.Max() == 0 ? 1 : counts.Max();

            for (int b = 0; b < bins; b++)
            {
                float bH = (float)counts[b] / maxCount * h;
                float x = padL + b * binW;
                float midScr = b * bucketSz + bucketSz / 2f;

                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(x + 2, padT + h - bH, x + binW - 2, padT + h), 2),
                    new SKPaint { Color = SKColor.Parse(ScoreColor(midScr)), IsAntialias = true });

                if (counts[b] > 0)
                    DrawCanvasText(canvas, counts[b].ToString(), x + 3, padT + h - bH - 12, 7, "#555555");
            }

            for (int b = 0; b <= bins; b += 2)
                DrawCanvasText(canvas, (b * bucketSz).ToString("F0"),
                    padL + b * binW - 6, padT + h + 5, 7, "#888888");

            // Marker for selected city
            float mx = padL + Math.Clamp(markerValue, 0, 100) / 100f * w;
            canvas.DrawLine(mx, padT, mx, padT + h,
                new SKPaint
                {
                    Color = SKColor.Parse(CityPalette[0]),
                    StrokeWidth = 2f,
                    PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
                });
            DrawCanvasText(canvas, $"^{markerValue:F1}", mx - 12, padT - 1, 7, CityPalette[0], bold: true);
        }

        void DrawRolePillarHeatmap(
            IContainer container,
            List<(string Role, List<PeerCityHistoryReportDto> Cities)> roles)
        {
            var pillars = roles
                .SelectMany(r => r.Cities)
                .SelectMany(c => c.CityHistory ?? new())
                .SelectMany(h => h.Pillars ?? new())
                .GroupBy(p => p.PillarID)
                .Select(g => g.First())
                .OrderBy(p => p.DisplayOrder)
                .Take(MaxPillars)
                .ToList();

            if (!pillars.Any()) return;

            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(90);
                    foreach (var _ in pillars) cols.RelativeColumn();
                });

                table.Cell().Background("#12352f").Padding(5)
                    .Text("Role / Pillar").FontSize(7).Bold().FontColor(Colors.White);
                foreach (var p in pillars)
                    table.Cell().Background("#12352f").Padding(4).AlignCenter()
                        .Text(Shorten(p.PillarName, 8))
                        .FontSize(7).Bold().FontColor(Colors.White);

                foreach (var (role, cities) in roles)
                {
                    table.Cell().Background("#f4f7f5").BorderBottom(0.5f).BorderColor("#e0e0e0")
                        .Padding(5).Text(role).FontSize(7).FontColor("#333333");

                    foreach (var pillar in pillars)
                    {
                        // include 0-score entries
                        var validScores = cities
                            .SelectMany(c => c.CityHistory ?? new())
                            .SelectMany(h => h.Pillars ?? new())
                            .Where(p => p.PillarID == pillar.PillarID)
                            .Select(p => (float)p.ScoreProgress)
                            .ToList();

                        bool hasData = validScores.Any();
                        float avg = hasData ? validScores.Average() : -1f;

                        string bg = !hasData ? "#f0f0f0"
                            : InterpolateColor("#ffffff", "#12352f", avg / 100f);

                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e0e0e0")
                            .Padding(3).AlignCenter()
                            .Text(!hasData ? "—" : $"{avg:F0}")
                            .FontSize(7).FontColor(avg >= 50 ? Colors.White : "#333333");
                    }
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  SHARED LAYOUT HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        void DrawInsightBand(IContainer container, string text)
        {
            container.Background("#e8f5e9").Padding(8)
                .Text(text).FontSize(8.5f).FontColor("#12352f");
        }

        void DrawNoDataPage(IContainer container)
        {
            container.AlignCenter().AlignMiddle()
                .Text("No data available for this section.")
                .FontSize(12).FontColor("#aaaaaa");
        }
        void DrawLegend(IContainer container, (string Color, string Label)[] items, int textsize = 20)
        {
            var groups = items
                .Select((x, i) => new { x, i })
                .GroupBy(x => x.i / 7)
                .Select(g => g.Select(v => v.x).ToList());

            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.AutoItem().AlignMiddle()
                        .Text("Legend:  ")
                        .FontSize(8)
                        .FontColor("#888888");
                });

                foreach (var group in groups)
                {
                    col.Item().Row(row =>
                    {
                        foreach (var (color, label) in group)
                        {
                            row.ConstantItem(10).Height(10).Canvas((c, s) =>
                                c.DrawRoundRect(
                                    new SKRoundRect(new SKRect(0, 1, 10, 9), 2),
                                    new SKPaint { Color = SKColor.Parse(color) }));

                            row.AutoItem()
                                .PaddingLeft(3)
                                .PaddingRight(10)
                                .Text(Shorten(label, textsize))
                                .FontSize(8)
                                .FontColor("#555555");
                        }
                    });
                }
            });
        }

        /// <summary>City-specific legend: gold dot for selected city, palette dots for peers.</summary>
        void DrawCityLineLegend(
            IContainer container,
            PeerCityHistoryReportDto mainCity,
            List<PeerCityHistoryReportDto> peers,
            AiCitySummeryDto cityDetails)
        {
            var items = new List<(string Color, string Label)>
            {
                (CityPalette[0],  $"{cityDetails.CityName} (selected)"),
                ("#4CAF8A",       "Peer Average")
            };
            for (int i = 0; i < peers.Count; i++)
                items.Add((CityPalette[1 + (i % (CityPalette.Length - 1))], peers[i].CityName));

            DrawLegend(container, items.ToArray());
        }

        /// <summary>Legend row showing a coloured dot for every city in the chart.</summary>
        void DrawCityLegend(
            IContainer container,
            List<PeerCityHistoryReportDto> allCities,
            AiCitySummeryDto cityDetails)
        {
            var items = allCities
                .Select((c, i) => (
                    Color: IsSameCity(c.CityName, cityDetails.CityName)
                        ? CityPalette[0]
                        : CityPalette[1 + (i % (CityPalette.Length - 1))],
                    Label: c.CityName
                ))
                .ToArray();

            DrawLegend(container, items);
        }

        static void DrawTableHeader(TableDescriptor table, string[] headers)
        {
            foreach (string h in headers)
                table.Cell().Background("#12352f").Padding(5)
                    .Text(h).FontSize(8).Bold().FontColor(Colors.White);
        }

        static void DrawPolyline(SKCanvas canvas, List<SKPoint> pts, SKPaint paint)
        {
            for (int i = 0; i < pts.Count - 1; i++)
                canvas.DrawLine(pts[i], pts[i + 1], paint);
        }

        static void DrawDashedPolyline(SKCanvas canvas, List<SKPoint> pts, SKPaint paint)
        {
            var dashed = paint.Clone();
            dashed.PathEffect = SKPathEffect.CreateDash(new[] { 5f, 3f }, 0);
            DrawPolyline(canvas, pts, dashed);
        }

        static void DrawCanvasText(
            SKCanvas canvas, string text, float x, float y, float textSize,
            string hexColor, bool bold = false)
        {
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(hexColor),
                TextSize = textSize,
                IsAntialias = true,
                Typeface = bold
                    ? SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                    : SKTypeface.FromFamilyName("Arial")
            };
            canvas.DrawText(text, x, y + textSize, paint);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  UTILITY HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the latest year's score including 0.
        /// Returns -1 only when there is genuinely NO history entry at all.
        /// </summary>
        static float GetLatestScoreOrZero(PeerCityHistoryReportDto city)
        {
            var last = city.CityHistory?
                .OrderByDescending(h => h.Year)
                .FirstOrDefault();
            return last != null ? (float)last.ScoreProgress : -1f;
        }

        /// <summary>Returns main-city entry from the combined list; null if not found.</summary>
        static PeerCityHistoryReportDto? FindMainCity(
            List<PeerCityHistoryReportDto> all, AiCitySummeryDto cityDetails) =>
            all.FirstOrDefault(p => IsSameCity(p.CityName, cityDetails.CityName));

        /// <summary>Case-insensitive city name equality check.</summary>
        static bool IsSameCity(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Builds a deduplicated list: main city first, then peers.</summary>
        static List<PeerCityHistoryReportDto> BuildAllCities(
            PeerCityHistoryReportDto? main,
            List<PeerCityHistoryReportDto> peers)
        {
            var list = new List<PeerCityHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }

        static string ScoreColor(float score) =>
            score >= 70 ? "#336b58" : score >= 40 ? "#f5a623" : "#e05252";

        static string DeriveRole(PeerCityHistoryReportDto city)
        {
            if (city.Population >= 5_000_000) return "Metropolis";
            if (city.Population >= 1_000_000) return "Major City";
            if (city.Population >= 300_000) return "Mid-Sized City";
            if (city.Population >= 100_000) return "Large Town";
            return "Small City";
        }

        static string FormatPop(decimal? value)
        {
            if (!value.HasValue || value <= 0) return "N/A";

            if (value >= 1_000_000_000) return $"{value / 1_000_000_000M:F1}B";
            if (value >= 1_000_000) return $"{value / 1_000_000M:F1}M";
            if (value >= 1_000) return $"{value / 1_000M:F0}K";

            return value.Value.ToString("N0");
        }

      

        static string InterpolateColor(string from, string to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var c1 = SKColor.Parse(from);
            var c2 = SKColor.Parse(to);
            byte r = (byte)(c1.Red + (c2.Red - c1.Red) * t);
            byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
            byte b = (byte)(c1.Blue + (c2.Blue - c1.Blue) * t);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }

}
