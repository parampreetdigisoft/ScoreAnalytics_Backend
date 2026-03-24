// ═══════════════════════════════════════════════════════════════════════════
//  PdfGeneratorService.ChartBridge.cs
//
//  PURPOSE:
//  DocxGeneratorService reuses every SkiaSharp chart painter that already
//  lives in PdfGeneratorService.  Those painters are currently private static
//  methods.  This partial-class file promotes them to internal static so
//  DocxGeneratorService can call them without duplicating code.
//
//  STEPS TO INTEGRATE:
//  1.  Add this file to your project alongside PdfGeneratorService.cs.
//  2.  In PdfGeneratorService.cs change each listed static method signature
//      from "static void PaintDonut(…)" → "internal static void PaintDonutPublic(…)"
//      (or just add the thin wrappers below and leave originals unchanged).
//  3.  Both services now compile against the same rendering logic.
//
//  WHY NOT INHERIT?
//  PdfGeneratorService uses QuestPDF's IDocumentContainer which is unrelated
//  to Word document structure.  A shared static helper is the cleanest seam.
// ═══════════════════════════════════════════════════════════════════════════

using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Services;
using SkiaSharp;
using QPDF = QuestPDF.Infrastructure;

namespace AssessmentPlatform.Common.Implementation
{
    // ── Option A (RECOMMENDED): Add thin public wrappers to PdfGeneratorService ──
    //
    // Open PdfGeneratorService.cs and add this partial class file.
    // Each method below delegates to the existing private static.

    public partial class PdfGeneratorService
    {
        // ── Donut / gauge chart ──────────────────────────────────────────────
        /// <param name="score">0–100 city progress score.</param>
        internal static void PaintDonutPublic(SKCanvas c, QPDF.Size s, float score)
            => PaintDonut(c, s, score);

        // ── Spider / radar chart ─────────────────────────────────────────────
        internal static void PaintSpiderChartPublic(SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars)
            => PaintSpiderChart(c, s, pillars);

        // ── KPI sparkline (area-gradient line) ──────────────────────────────
        internal static void PaintKpiSparklinePublic(SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis)
            => PaintKpiSparkline(c, s, kpis);

        // ── KPI bar chart (vertical bars with numbered x-axis) ──────────────
        /// <param name="offset">Global KPI index offset for sequential numbering.</param>
        internal static void DrawKpiBarChartCanvas(
            SKCanvas c, QPDF.Size s, List<KpiChartItem> data, int offset)
        {
            // PdfGeneratorService.DrawKpiBarChart uses IContainer (QuestPDF).
            // Replicate the canvas-only portion here since it is already
            // purely SkiaSharp.  This is the ONLY method that needs a copy
            // because the original wraps an IContainer.Background().Canvas() call.

            if (!data.Any()) return;

            const float lp = 8f, rp = 8f, tp = 22f, bp = 26f;
            float chartW = s.Width - lp - rp;
            float chartH = s.Height - tp - bp;
            int n = data.Count;
            float barW  = chartW / n;
            float innerW = barW * 0.62f;
            float barGap = (barW - innerW) / 2f;

            using var gridPaint = new SKPaint { Color = SKColor.Parse("#F2F7F4"), StrokeWidth = 0.6f };
            using var gridLbl   = new SKPaint { Color = SKColor.Parse("#B0BEC5"), TextSize = 7f, IsAntialias = true, TextAlign = SKTextAlign.Left };
            foreach (float pct in new[] { 25f, 50f, 75f, 100f })
            {
                float gy = tp + chartH - pct / 100f * chartH;
                c.DrawLine(lp, gy, lp + chartW, gy, gridPaint);
                c.DrawText($"{(int)pct}", lp + 2, gy - 2, gridLbl);
            }

            float y70 = tp + chartH - 0.70f * chartH;
            using var thPaint = new SKPaint
            {
                Color = SKColor.Parse("#2E7D32").WithAlpha(100), StrokeWidth = 0.9f,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0), IsAntialias = true
            };
            c.DrawLine(lp, y70, lp + chartW, y70, thPaint);

            using var valLbl = new SKPaint { TextSize = 6.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };
            using var numLbl = new SKPaint { Color = SKColor.Parse("#546E7A"), TextSize = 6.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            for (int i = 0; i < n; i++)
            {
                float v  = (float)(data[i].Value ?? 0);
                float bx = lp + i * barW + barGap;
                float bh = v / 100f * chartH;
                float by = tp + chartH - bh;
                SKColor color = GetColorStatic(v);
                SKColor textColor = v > 85 ? SKColors.White : SKColors.Black;

                using var ghost = new SKPaint { Color = color.WithAlpha(35), IsAntialias = true };
                c.DrawRoundRect(new SKRoundRect(new SKRect(bx, tp, bx + innerW, tp + chartH), 2), ghost);

                using var shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, by), new SKPoint(0, tp + chartH),
                    new[] { color, color.WithAlpha(180) }, null, SKShaderTileMode.Clamp);
                using var barPaint = new SKPaint { Shader = shader, IsAntialias = true };
                c.DrawRoundRect(new SKRoundRect(new SKRect(bx, by, bx + innerW, tp + chartH), 2), barPaint);

                using var cap = new SKPaint { Color = color, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
                c.DrawLine(bx + 1, by, bx + innerW - 1, by, cap);

                float vly = by - 3f;
                if (vly < tp + 8f) vly = by + 10f;
                valLbl.Color = textColor;
                c.DrawText($"{v:F1}%", bx + innerW / 2f, vly, valLbl);
                c.DrawText($"{offset + i + 1}", bx + innerW / 2f, s.Height - 6f, numLbl);
            }
        }

        // ── Radial / concentric-ring pillar chart ────────────────────────────
        internal static void DrawPillarsRadialChartCanvas(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).Take(14).ToList();
            if (!data.Any()) return;
            float cx = s.Width / 2f, cy = s.Height / 2f;
            float maxR = Math.Min(cx, cy) - 18f;
            float minR = maxR * 0.28f;
            float step = (maxR - minR) / data.Count;
            float thick = step * 0.68f;
            float avg   = (float)data.Average(x => x.Value ?? 0);

            using var title = new SKPaint { Color = SKColor.Parse("#12352f"), TextSize = 10f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
            c.DrawText("Pillar Performance", cx, 14f, title);

            for (int i = 0; i < data.Count; i++)
            {
                float v   = (float)(data[i].Value ?? 0);
                float r   = maxR - i * step;
                float mid = r - thick / 2f;
                var rect  = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);
                SKColor col = GetColorStatic(v);

                using var track = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = thick, Color = col.WithAlpha(22), IsAntialias = true };
                c.DrawOval(rect, track);
                using var arc = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = thick, Color = col, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
                c.DrawArc(rect, -90f, 360f * v / 100f, false, arc);

                float la = (-90f + 360f * v / 100f) * (float)Math.PI / 180f;
                using var dot = new SKPaint { Color = col, Style = SKPaintStyle.Fill, IsAntialias = true };
                c.DrawCircle(cx + mid * (float)Math.Cos(la), cy + mid * (float)Math.Sin(la), thick / 2f + 1.5f, dot);
            }

            float cr = minR - step * 0.6f;
            using var fill = new SKPaint { Color = SKColor.Parse("#12352f"), Style = SKPaintStyle.Fill, IsAntialias = true };
            c.DrawCircle(cx, cy, cr, fill);
            using var numP = new SKPaint { Color = GetColorStatic(avg), TextSize = cr * 0.60f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
            c.DrawText($"{avg:F0}", cx, cy + numP.TextSize * 0.36f, numP);
        }

        // ── Horizontal bar list for pillars ─────────────────────────────────
        internal static void DrawPillarHorizontalBarsCanvas(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars)
        {
            var sorted = pillars.OrderByDescending(x => x.Value).ToList();
            float rowH  = s.Height / Math.Max(sorted.Count, 1);
            float labelW = 110f;
            float barArea = s.Width - labelW - 50f;

            using var lbl = new SKPaint { Color = SKColor.Parse("#37474F"), TextSize = 8.5f, IsAntialias = true };
            for (int i = 0; i < sorted.Count; i++)
            {
                float v  = (float)(sorted[i].Value ?? 0);
                float y  = i * rowH;
                float bw = v / 100f * barArea;
                SKColor col = GetColorStatic(v);

                if (i % 2 == 0) c.DrawRect(new SKRect(0, y, s.Width, y + rowH), new SKPaint { Color = SKColor.Parse("#F4F7F5") });

                c.DrawText(Shorten(sorted[i].Name ?? "—", 16), 4, y + rowH * 0.65f, lbl);
                using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(bw, 0),
                    new[] { col.WithAlpha(210), col }, null, SKShaderTileMode.Clamp);
                using var bar = new SKPaint { Shader = shader, IsAntialias = true };
                c.DrawRoundRect(new SKRoundRect(new SKRect(labelW, y + 3, labelW + bw, y + rowH - 3), 3), bar);
                using var scorePaint = new SKPaint { Color = col, TextSize = 8.5f, IsAntialias = true, TextAlign = SKTextAlign.Left };
                c.DrawText($"{v:F1}%", labelW + bw + 5, y + rowH * 0.65f, scorePaint);
            }
        }

        // ── Population bar chart (canvas-only, no IContainer wrapper) ───────
        internal static void DrawPopulationBarsCanvas(
            SKCanvas c, QPDF.Size s,
            List<PeerCityHistoryReportDto> cities, AiCitySummeryDto cityDetails)
        {
            if (!cities.Any()) return;
            long maxPop = (long)(cities.Max(p => p.Population) ?? 1);
            float rowH   = s.Height / Math.Max(cities.Count, 1);
            float labelW = 130f, barArea = s.Width - labelW - 72f;
            string[] palette = { "F0B429", "4CAF8A", "1E88E5", "FB8C00", "7B61FF", "E05252" };

            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                float y  = i * rowH;
                float bw = (float)((city.Population ?? 0) / (double)maxPop * barArea);
                bool isMain = IsSameCityStatic(city.CityName, cityDetails.CityName);
                if (i % 2 == 0) c.DrawRect(new SKRect(0, y, s.Width, y + rowH), new SKPaint { Color = SKColor.Parse("#F4F7F5") });
                if (isMain)     c.DrawRect(new SKRect(0, y, s.Width, y + rowH), new SKPaint { Color = SKColor.Parse("#FFF8E1") });

                using var txt = new SKPaint { Color = SKColor.Parse(isMain ? "#12352f" : "#444444"), TextSize = 9f, IsAntialias = true, FakeBoldText = isMain };
                c.DrawText(city.CityName, 4, y + rowH * 0.65f, txt);
                string clr = isMain ? palette[0] : palette[1 + (i % (palette.Length - 1))];
                c.DrawRoundRect(new SKRoundRect(new SKRect(labelW, y + 4, labelW + bw, y + rowH - 6), 3), new SKPaint { Color = SKColor.Parse(clr), IsAntialias = true });
                using var num = new SKPaint { Color = SKColor.Parse("#555555"), TextSize = 9f, IsAntialias = true };
                c.DrawText(FormatPopStatic(city.Population), labelW + bw + 5, y + rowH * 0.65f, num);
            }
        }

        // ── Regional score bars (canvas only) ───────────────────────────────
        internal static void DrawRegionalBarsCanvas(
            SKCanvas c, QPDF.Size s, List<PeerCityHistoryReportDto> all)
        {
            var byRegion = all
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Region) ? p.Country ?? "Unknown" : p.Region)
                .OrderByDescending(g => g.Count()).ToList();

            float barH   = s.Height / Math.Max(byRegion.Count, 1);
            float labelW = 120f, barArea = s.Width - labelW - 65f;
            string[] palette = { "12352F","336B58","4CAF8A","F0B429","E05252","7B61FF","1E88E5" };

            for (int i = 0; i < byRegion.Count; i++)
            {
                var g    = byRegion[i];
                float y  = i * barH;
                float avg = (float)g.Average(city => GetLatestScoreOrZeroStatic(city));
                if (avg < 0) avg = 0;
                float bw = avg / 100f * barArea;
                if (i % 2 == 0) c.DrawRect(new SKRect(0, y, s.Width, y + barH), new SKPaint { Color = SKColor.Parse("#F4F7F5") });
                using var lbl = new SKPaint { Color = SKColor.Parse("#333333"), TextSize = 9f, IsAntialias = true };
                c.DrawText(g.Key, 4, y + barH * 0.65f, lbl);
                c.DrawRoundRect(new SKRoundRect(new SKRect(labelW, y + 3, labelW + bw, y + barH - 6), 3),
                    new SKPaint { Color = SKColor.Parse(palette[i % palette.Length]), IsAntialias = true });
                using var sc = new SKPaint { Color = SKColor.Parse("#555555"), TextSize = 9f, IsAntialias = true };
                c.DrawText($"{avg:F1}  (n={g.Count()})", labelW + bw + 5, y + barH * 0.65f, sc);
            }
        }

        // ── Multi-line trend chart ────────────────────────────────────────────
        internal static void DrawMultiLineTrendChartCanvas(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCityHistoryReportDto> peers,
            PeerCityHistoryReportDto? mainCity, AiCitySummeryDto cityDetails,
            List<(int Year, float Avg, bool HasData)> peerAvg)
        {
            if (years.Count < 2) return;
            const float padL = 36f, padR = 12f, padT = 10f, padB = 24f;
            float w = s.Width - padL - padR, h = s.Height - padT - padB;
            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            using var grid  = new SKPaint { Color = SKColor.Parse("#e8e8e8"), StrokeWidth = 0.5f };
            using var glbl  = new SKPaint { Color = SKColor.Parse("#aaaaaa"), TextSize = 7f, IsAntialias = true };
            foreach (int sc in new[] { 0, 25, 50, 75, 100 }) { float y = Yp(sc); c.DrawLine(padL, y, padL + w, y, grid); c.DrawText(sc.ToString(), 2, y - 5, glbl); }
            foreach (int yr in years)                          { float x = Xp(yr); c.DrawLine(x, padT, x, padT + h, new SKPaint { Color = SKColor.Parse("#F0F0F0"), StrokeWidth = 0.5f }); c.DrawText(yr.ToString(), x - 14, padT + h + 7, glbl); }

            // Peer lines
            string[] pal = { "F0B429","4CAF8A","1E88E5","FB8C00","7B61FF","E05252" };
            for (int pi = 0; pi < peers.Count; pi++)
            {
                var pts = (peers[pi].CityHistory ?? new()).Where(h => years.Contains(h.Year)).OrderBy(h => h.Year)
                    .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress))).ToList();
                DrawPolylineStatic(c, pts, new SKPaint { Color = SKColor.Parse(pal[1 + (pi % (pal.Length - 1))]).WithAlpha(180), StrokeWidth = 1.2f, IsAntialias = true, IsStroke = true });
            }

            // Main city line
            var mainPts = (mainCity?.CityHistory ?? new()).Where(h => years.Contains(h.Year)).OrderBy(h => h.Year)
                .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress))).ToList();
            DrawPolylineStatic(c, mainPts, new SKPaint { Color = SKColor.Parse(pal[0]), StrokeWidth = 2.5f, IsAntialias = true, IsStroke = true });
            foreach (var pt in mainPts) c.DrawCircle(pt.X, pt.Y, 4f, new SKPaint { Color = SKColor.Parse(pal[0]), IsAntialias = true });
        }

        // ── Pillar line chart ─────────────────────────────────────────────────
        internal static void DrawPillarLineChartCanvas(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCityYearHistoryDto> history,
            List<PeerCityPillarHistoryReportDto> pillars)
        {
            if (years.Count < 2) return;
            string[] pal = { "12352F","336B58","4CAF8A","F0B429","F5A623","E05252","7B61FF","1E88E5","43A047","FB8C00","0097A7","8D6E63","E91E63","607D8B" };
            const float padL = 36f, padR = 10f, padT = 8f, padB = 20f;
            float w = s.Width - padL - padR, h = s.Height - padT - padB;
            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            using var grid = new SKPaint { Color = SKColor.Parse("#e8e8e8"), StrokeWidth = 0.5f };
            using var glbl = new SKPaint { Color = SKColor.Parse("#aaaaaa"), TextSize = 7f, IsAntialias = true };
            foreach (int sc in new[] { 0, 25, 50, 75, 100 }) { float y = Yp(sc); c.DrawLine(padL, y, padL + w, y, grid); c.DrawText(sc.ToString(), 2, y - 5, glbl); }
            foreach (int yr in years) c.DrawText(yr.ToString(), Xp(yr) - 12, padT + h + 5, glbl);

            for (int pi = 0; pi < pillars.Count; pi++)
            {
                string color = pal[pi % pal.Length];
                var pts = years.Select(yr =>
                {
                    var hEntry = history.FirstOrDefault(h => h.Year == yr);
                    var ps     = hEntry?.Pillars?.FirstOrDefault(p => p.PillarID == pillars[pi].PillarID);
                    return ps != null ? (float?)ps.ScoreProgress : null;
                })
                .Select((sc, idx) => (Year: years[idx], Score: sc))
                .Where(p => p.Score.HasValue)
                .Select(p => new SKPoint(Xp(p.Year), Yp(p.Score!.Value)))
                .ToList();

                DrawPolylineStatic(c, pts, new SKPaint { Color = SKColor.Parse(color), StrokeWidth = 1.5f, IsAntialias = true, IsStroke = true });
                foreach (var pt in pts) c.DrawCircle(pt.X, pt.Y, 2.5f, new SKPaint { Color = SKColor.Parse(color), IsAntialias = true });
            }
        }

        // ── Private static helpers shared by canvas methods above ─────────────

        private static SKColor GetColorStatic(float v)
            => v >= 70 ? SKColor.Parse("#2E7D32") : v >= 40 ? SKColor.Parse("#F9A825") : SKColor.Parse("#C62828");

        private static bool IsSameCityStatic(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static float GetLatestScoreOrZeroStatic(PeerCityHistoryReportDto city)
        {
            var last = city.CityHistory?.OrderByDescending(h => h.Year).FirstOrDefault();
            return last != null ? (float)last.ScoreProgress : -1f;
        }

        private static string FormatPopStatic(decimal? value)
        {
            if (!value.HasValue || value <= 0) return "N/A";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000M:F1}B";
            if (value >= 1_000_000)     return $"{value / 1_000_000M:F1}M";
            if (value >= 1_000)         return $"{value / 1_000M:F0}K";
            return value.Value.ToString("N0");
        }

        private static void DrawPolylineStatic(SKCanvas c, List<SKPoint> pts, SKPaint paint)
        {
            for (int i = 0; i < pts.Count - 1; i++) c.DrawLine(pts[i], pts[i + 1], paint);
        }

        //private static string Shorten(string text, int max)
        //    => string.IsNullOrWhiteSpace(text) ? "" : text.Length <= max ? text : text[..max] + "…";

        
        // ── Scatter plot (population or income vs score) ─────────────────────
        internal static void DrawScatterPlotCanvas(
            SKCanvas c, QPDF.Size s,
            List<PeerCityHistoryReportDto> cities,
            AiCitySummeryDto cityDetails,
            Func<PeerCityHistoryReportDto, float> xVal,
            Func<PeerCityHistoryReportDto, float> yVal,
            string xLabel, string yLabel)
        {
            if (!cities.Any()) return;

            string[] palette = { "#F0B429", "#4CAF8A", "#1E88E5", "#FB8C00", "#7B61FF", "#E05252" };

            const float padL = 42f, padR = 14f, padT = 12f, padB = 28f;
            float w = s.Width - padL - padR;
            float h = s.Height - padT - padB;

            float xMin = cities.Min(xVal);
            float xMax = cities.Max(xVal);
            if (xMax <= xMin) xMax = xMin + 1;

            float Xp(float v) => padL + (v - xMin) / (xMax - xMin) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            // Grid + axes
            using var axis = new SKPaint { Color = SKColor.Parse("#AAAAAA"), StrokeWidth = 0.8f };
            using var grid = new SKPaint { Color = SKColor.Parse("#EEEEEE"), StrokeWidth = 0.5f };
            using var glbl = new SKPaint { Color = SKColor.Parse("#999999"), TextSize = 7f, IsAntialias = true };
            c.DrawLine(padL, padT, padL, padT + h, axis);
            c.DrawLine(padL, padT + h, padL + w, padT + h, axis);
            foreach (int sc in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(sc);
                c.DrawLine(padL, y, padL + w, y, grid);
                c.DrawText(sc.ToString(), 2, y - 2, glbl);
            }

            // Dots
            for (int i = 0; i < cities.Count; i++)
            {
                bool isMain = IsSameCityStatic(cities[i].CityName, cityDetails.CityName);
                float x = Xp(xVal(cities[i]));
                float y = Yp(yVal(cities[i]));
                string clr = isMain ? palette[0] : palette[1 + (i % (palette.Length - 1))];

                using var dot = new SKPaint { Color = SKColor.Parse(clr), IsAntialias = true };
                c.DrawCircle(x, y, isMain ? 6.5f : 4.5f, dot);

                if (isMain)
                {
                    using var ring = new SKPaint
                    {
                        Color = SKColor.Parse("#12352F"),
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1.8f,
                        IsAntialias = true
                    };
                    c.DrawCircle(x, y, 7.5f, ring);
                }
            }

            // Axis labels
            using var xlbl = new SKPaint { Color = SKColor.Parse("#666666"), TextSize = 8f, IsAntialias = true, TextAlign = SKTextAlign.Center };
            using var ylbl = new SKPaint { Color = SKColor.Parse("#666666"), TextSize = 8f, IsAntialias = true };
            c.DrawText(xLabel, padL + w / 2f, padT + h + 20, xlbl);
            c.DrawText(yLabel, 2, padT + h / 2f, ylbl);
        }

        // ── Income quartile vertical bars ─────────────────────────────────────
        internal static void DrawIncomeQuartileBarsCanvas(
            SKCanvas c, QPDF.Size s,
            List<PeerCityHistoryReportDto> all)
        {
            string[] categoryOrder = { "Low Income", "Lower-Middle Income", "Upper-Middle Income", "High Income" };
            string[] segColors = { "#D9534F", "#F0AD4E", "#5BC0DE", "#2E7D32" };

            var segments = all
                .GroupBy(x => PdfGeneratorService.GetIncomeCategory(x.Income ?? 0))
                .ToDictionary(g => g.Key, g => g.ToList());

            float totalW = s.Width - 40f;
            float slotW = totalW / 4f;
            float barW = slotW * 0.62f;
            float baseY = s.Height - 32f;
            float maxBarH = baseY - 10f;

            using var valPaint = new SKPaint { TextSize = 9f, IsAntialias = true, FakeBoldText = true, TextAlign = SKTextAlign.Center };
            using var catPaint = new SKPaint { Color = SKColor.Parse("#555555"), TextSize = 7f, IsAntialias = true };
            using var nPaint = new SKPaint { Color = SKColor.Parse("#888888"), TextSize = 7f, IsAntialias = true };

            for (int i = 0; i < categoryOrder.Length; i++)
            {
                string label = categoryOrder[i];
                float slotX = 20f + i * slotW;
                float barX = slotX + (slotW - barW) / 2f;

                if (!segments.TryGetValue(label, out var cities) || !cities.Any())
                {
                    using var noData = new SKPaint { Color = SKColor.Parse("#DDDDDD"), IsAntialias = true };
                    c.DrawRoundRect(new SKRoundRect(new SKRect(barX, baseY - 4, barX + barW, baseY), 4), noData);
                    c.DrawText(label.Length > 14 ? label[..14] + "…" : label, slotX, baseY + 12, catPaint);
                    continue;
                }

                float avg = cities.Average(city => { float sc = GetLatestScoreOrZeroStatic(city); return sc < 0 ? 0 : sc; });
                float barH = avg / 100f * maxBarH;

                using var barPaint = new SKPaint { Color = SKColor.Parse(segColors[i]), IsAntialias = true };
                c.DrawRoundRect(new SKRoundRect(new SKRect(barX, baseY - barH, barX + barW, baseY), 6), barPaint);

                valPaint.Color = SKColor.Parse("#12352F");
                c.DrawText($"{avg:F1}", barX + barW / 2f, baseY - barH - 5, valPaint);

                string shortLabel = label.Length > 14 ? label[..14] + "…" : label;
                c.DrawText(shortLabel, slotX, baseY + 12, catPaint);
                c.DrawText($"n={cities.Count}", slotX, baseY + 22, nPaint);
            }
        }

        // Add this alongside the other internal helpers at the bottom of the partial class
        /// <summary>Exposed so DocxGeneratorService can pass it as a Func delegate.</summary>
        internal static float GetLatestScoreOrZeroForDocx(PeerCityHistoryReportDto city)
            => GetLatestScoreOrZeroStatic(city);

        // ── Score distribution histogram ──────────────────────────────────────
        internal static void DrawHistogramCanvas(
            SKCanvas c, QPDF.Size s,
            List<float> scores, float markerValue, int bins = 10)
        {
            if (!scores.Any()) return;

            const float padL = 32f, padR = 10f, padT = 14f, padB = 22f;
            float w = s.Width - padL - padR;
            float h = s.Height - padT - padB;
            float binW = w / bins;
            float bucket = 100f / bins;

            int[] counts = new int[bins];
            foreach (float sc in scores)
                counts[Math.Clamp((int)(sc / bucket), 0, bins - 1)]++;
            int maxCnt = counts.Max() == 0 ? 1 : counts.Max();

            using var axisLbl = new SKPaint { Color = SKColor.Parse("#888888"), TextSize = 7f, IsAntialias = true };
            using var cntLbl = new SKPaint { Color = SKColor.Parse("#555555"), TextSize = 7f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            for (int b = 0; b < bins; b++)
            {
                float bH = (float)counts[b] / maxCnt * h;
                float x = padL + b * binW;
                float midSc = b * bucket + bucket / 2f;

                using var bar = new SKPaint { Color = GetColorStatic(midSc), IsAntialias = true };
                c.DrawRoundRect(new SKRoundRect(new SKRect(x + 2, padT + h - bH, x + binW - 2, padT + h), 3), bar);

                if (counts[b] > 0)
                    c.DrawText(counts[b].ToString(), x + binW / 2f, padT + h - bH - 3, cntLbl);
            }

            // X-axis labels every 2 bins
            for (int b = 0; b <= bins; b += 2)
                c.DrawText((b * bucket).ToString("F0"), padL + b * binW - 5, padT + h + 14, axisLbl);

            // Selected-city marker (dashed gold line)
            float mx = padL + Math.Clamp(markerValue, 0, 100) / 100f * w;
            using var marker = new SKPaint
            {
                Color = SKColor.Parse("#F0B429"),
                StrokeWidth = 2f,
                PathEffect = SKPathEffect.CreateDash(new[] { 5f, 3f }, 0)
            };
            c.DrawLine(mx, padT, mx, padT + h, marker);

            using var mLbl = new SKPaint
            {
                Color = SKColor.Parse("#F0B429"),
                TextSize = 7.5f,
                IsAntialias = true,
                FakeBoldText = true,
                TextAlign = SKTextAlign.Center
            };
            c.DrawText($"▲{markerValue:F1}", mx, padT - 2, mLbl);
        }
    }
}
