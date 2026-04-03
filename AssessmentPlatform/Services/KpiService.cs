using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AssessmentPlatform.Services
{
    public class KpiService : IKpiService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public KpiService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        #region GetAnalyticalLayerResults
        public async Task<PaginationResponse<GetAnalyticalLayerResultDto>> 
            GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending)
        {
            try
            {
                var year = request.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                var baseQuery = _context.AnalyticalLayerResults
                    .AsNoTracking()
                    .Include(ar => ar.AnalyticalLayer)
                        .ThenInclude(al => al.FiveLevelInterpretations)
                    .Include(ar => ar.City)
                    .Where(x => (x.LastUpdated >= startDate && x.LastUpdated < endDate) || (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate));

                if (role == UserRole.CityUser)
                {
                    var validCities = _context.PublicUserCityMappings
                        .Where(x =>
                            x.IsActive &&
                            x.UserID == userId &&
                            (!request.CityID.HasValue || x.CityID == request.CityID))
                        .Select(x => x.CityID);

                    var validPillarIds = _context.CityUserPillarMappings
                        .Where(x => x.IsActive && x.UserID == userId)
                        .Select(x => x.PillarID);

                    var validLayerIds = _context.AnalyticalLayerPillarMappings
                        .Where(x =>
                            validPillarIds.Contains(x.PillarID) &&
                            (!request.LayerID.HasValue || x.LayerID == request.LayerID))
                        .Select(x => x.LayerID)
                        .Distinct();

                    baseQuery = baseQuery
                        .Where(ar =>
                            validCities.Contains(ar.CityID) &&
                            validLayerIds.Contains(ar.LayerID));
                }
                else if (role == UserRole.Evaluator || role == UserRole.Analyst)
                {
                    var validCities = _context.UserCityMappings
                        .Where(x =>
                            x.UserID == userId &&
                            !x.IsDeleted &&
                            (!request.CityID.HasValue || x.CityID == request.CityID))
                        .Select(x => x.CityID);
                    baseQuery = baseQuery.Where(ar => validCities.Contains(ar.CityID) && (!request.LayerID.HasValue || ar.LayerID == request.LayerID));
                }
                else
                {
                    baseQuery = baseQuery.Where(ar =>
                        (!request.CityID.HasValue || ar.CityID == request.CityID) &&
                        (!request.LayerID.HasValue || ar.LayerID == request.LayerID));
                }
                var response = await baseQuery.Select(Projection).ApplyPaginationAsync(request);

                return response;

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAnalyticalLayers", ex);
                return new PaginationResponse<GetAnalyticalLayerResultDto>();
            }
        }

        private static Expression<Func<AnalyticalLayerResult, GetAnalyticalLayerResultDto>> Projection => ar => new GetAnalyticalLayerResultDto
        {
            LayerResultID = ar.LayerResultID,
            LayerID = ar.LayerID,
            CityID = ar.CityID,
            InterpretationID = ar.InterpretationID,
            NormalizeValue = ar.NormalizeValue,
            CalValue1 = ar.CalValue1,
            CalValue2 = ar.CalValue2,
            CalValue3 = ar.CalValue3,
            CalValue4 = ar.CalValue4,
            CalValue5 = ar.CalValue5,
            LastUpdated = ar.LastUpdated,

            AiInterpretationID = ar.AiInterpretationID,
            AiNormalizeValue = ar.AiNormalizeValue,
            AiCalValue1 = ar.AiCalValue1,
            AiCalValue2 = ar.AiCalValue2,
            AiCalValue3 = ar.AiCalValue3,
            AiCalValue4 = ar.AiCalValue4,
            AiCalValue5 = ar.AiCalValue5,
            AiLastUpdated = ar.AiLastUpdated,

            LayerCode = ar.AnalyticalLayer.LayerCode,
            LayerName = ar.AnalyticalLayer.LayerName,
            Purpose = ar.AnalyticalLayer.Purpose,
            CalText1 = ar.AnalyticalLayer.CalText1,
            CalText2 = ar.AnalyticalLayer.CalText2,
            CalText3 = ar.AnalyticalLayer.CalText3,
            CalText4 = ar.AnalyticalLayer.CalText4,
            CalText5 = ar.AnalyticalLayer.CalText5,
            Definition = ar.AnalyticalLayer.Definition,
            FiveLevelInterpretations = ar.AnalyticalLayer.FiveLevelInterpretations,

            City = ar.City
        };

        #endregion
        public async Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi(int userId, UserRole role)
        {
            try
            {
                IQueryable<AnalyticalLayer> query = _context.AnalyticalLayers
                    .Where(x => !x.IsDeleted);

                if (role == UserRole.CityUser)
                {
                    query =
                        from layer in _context.AnalyticalLayers
                        join map in _context.AnalyticalLayerPillarMappings
                            on layer.LayerID equals map.LayerID
                        join userMap in _context.CityUserPillarMappings
                            on map.PillarID equals userMap.PillarID
                        where !layer.IsDeleted
                              && userMap.IsActive
                              && userMap.UserID == userId
                        select layer;
                }

                var result = await query
                    .AsNoTracking()
                    .Distinct()
                    .ToListAsync();

                return ResultResponseDto<List<AnalyticalLayer>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAllKpi", ex);
                return ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "An error occurred" });
            }
        }
        public async Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, UserRole role, bool applyPagination = true)
        {
            try
            {
                var year = c.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);


                var validKpiIds = new List<int>();

                if (c.Kpis==null || c.Kpis.Count == 0)
                {
                    var query = _context.AnalyticalLayers
                        .Where(x => !x.IsDeleted)
                        .Select(x => x.LayerID)
                        .OrderBy(x => x);

                    if (applyPagination)
                    {
                        var res = await query.ApplyPaginationAsync(c);
                        validKpiIds = res.Data.ToList();
                    }
                    else
                    {
                        validKpiIds = await query.ToListAsync();
                    }
                }
                else
                {
                    validKpiIds = c.Kpis;
                }

                Expression<Func<City, bool>> expression = role switch
                {
                    UserRole.Admin => x => !x.IsDeleted && c.Cities.Contains(x.CityID),
                    UserRole.Analyst => x => !x.IsDeleted && c.Cities.Contains(x.CityID),
                    UserRole.Evaluator => x => !x.IsDeleted && c.Cities.Contains(x.CityID),
                    _ => x => false
                };

                // Step 2: Get all selected cities (even if no analytical data)
                var selectedCities = await _context.Cities
                    .Where(expression)
                    .Distinct()
                    .ToListAsync();

                var selectedCityIds = selectedCities.Select(x => x.CityID).ToList();

                if (role == UserRole.Analyst || role == UserRole.Evaluator)
                {
                    var validMappedCityIds = await _context.UserCityMappings
                       .Where(x => x.UserID == userId && !x.IsDeleted)
                       .Select(x => x.CityID)
                       .ToListAsync();

                    // ✅ Check if all selected cities are valid
                    bool allValid = selectedCityIds.All(id => validMappedCityIds.Contains(id));

                    if (!allValid)
                    {
                        return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "No valid cities found." });
                    }
                }

                // Step 3: Fetch analytical layer results for selected cities
                var analyticalResults = await _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                    .Where(x => selectedCityIds.Contains(x.CityID)
                    && ((x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate || x.LastUpdated >= startDate && x.LastUpdated < endDate))
                    && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.CityID,
                        ar.LayerID,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.AnalyticalLayer.Definition,
                        ar.CalValue5,
                        ar.AiCalValue5
                    })
                    .ToListAsync();

                // Step 4: Get all distinct layers
                var allLayers = analyticalResults
                    .Select(x => new { x.LayerID, x.LayerCode, x.LayerName,x.Definition })
                    .Distinct()
                    .OrderBy(x => x.LayerName)
                    .ToList();

                // Step 5: Prepare response DTO
                var response = new CompareCityResponseDto
                {
                    Categories = new List<string>(),
                    Series = new List<ChartSeriesDto>(),
                    TableData = new List<ChartTableRowDto>()
                };

                // Initialize chart series for each city
                foreach (var city in selectedCities)
                {
                    response.Series.Add(new ChartSeriesDto
                    {
                        Name = city.CityName,
                        Data = new List<decimal>(),
                        AiData = new List<decimal>()
                    });
                }

                // Add Peer City Score series
                var peerSeries = new ChartSeriesDto
                {
                    Name = "Peer City Score",
                    Data = new List<decimal>(),
                    AiData = new List<decimal>()
                };

                // Step 6: Build chart and table data
                foreach (var layer in allLayers)
                {
                    response.Categories.Add(layer.LayerCode);

                    // Map KPI values for each city (0 if missing)
                    var values = new Dictionary<int, List<decimal>>();

                    foreach (var city in selectedCities)
                    {
                        var value = analyticalResults
                            .FirstOrDefault(r => r.CityID == city.CityID && r.LayerID == layer.LayerID);

                        var evaluatedValue = Math.Round(value?.CalValue5 ?? 0, 2);
                        var aiValue = Math.Round(value?.AiCalValue5 ?? 0, 2);
                        values[city.CityID] = new List<decimal> { evaluatedValue, aiValue };

                        // Add to series
                        var citySeries = response.Series.First(s => s.Name == city.CityName);
                        citySeries.Data.Add(evaluatedValue);

                        citySeries.AiData.Add(aiValue);
                    }
                    // ✅ Calculate Peer City Score (average of all cities for this layer)
                    var peerCityScore = values.Values.Any() ? Math.Round(values.Values.Select(x => x.First()).Average(), 2) : 0;
                    peerSeries.Data.Add(peerCityScore);
                    var aiPeerCityScore = values.Values.Any() ? Math.Round(values.Values.Select(x => x.Last()).Average(), 2) : 0;
                    peerSeries.AiData.Add(aiPeerCityScore);

                    // Add table data
                    response.TableData.Add(new ChartTableRowDto
                    {
                        LayerID = layer.LayerID,
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
                        Definition = layer.Definition,
                        CityValues = selectedCities.Select(c => new CityValueDto
                        {
                            CityID = c.CityID,
                            CityName = c.CityName,
                            Value = values[c.CityID].First(),
                            AiValue = values[c.CityID].Last()
                        }).ToList(),
                        PeerCityScore = peerCityScore // You can rename property if needed
                    });
                }

                // Append Peer City Score series
                response.Series.Add(peerSeries);

                return ResultResponseDto<CompareCityResponseDto>.Success(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in CompareCities", ex);
                return ResultResponseDto<CompareCityResponseDto>.Failure(new List<string> { "An error occurred while comparing cities." });
            }
        }

        public async Task<ResultResponseDto<GetMutiplekpiLayerResultsDto>> GetMutiplekpiLayerResults(
            GetMutiplekpiLayerRequestDto request,
            int userId,
            UserRole role,
            TieredAccessPlan userPlan = TieredAccessPlan.Pending)
        {
            try
            {
                var year = request.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = startDate.AddYears(1);

                if (role == UserRole.CityUser)
                {
                    var validCityIds = await _context.PublicUserCityMappings
                        .Where(x =>
                            x.IsActive &&
                            x.UserID == userId)
                        .Select(x => x.CityID)
                        .ToListAsync();

                    bool hasInvalidCity = request.CityIDs
                        .Any(cityId => !validCityIds.Contains(cityId));

                    if (hasInvalidCity)
                    {
                        return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                            .Failure(new List<string> { "You are not authorized to access one or more selected cities." });
                    }
                }


                var query = _context.AnalyticalLayerResults
                    .AsNoTracking()
                    .Where(x =>
                        request.CityIDs.Contains(x.CityID) &&
                        x.LayerID == request.LayerID &&
                        (
                            (x.LastUpdated >= startDate && x.LastUpdated < endDate) ||
                            (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate)
                        ));

                var response = await query
                    .GroupBy(x => x.LayerID)
                    .Select(g => new GetMutiplekpiLayerResultsDto
                    {
                        LayerID = g.Key,

                        LayerCode = g.Select(x => x.AnalyticalLayer.LayerCode).FirstOrDefault()?? string.Empty,
                        LayerName = g.Select(x => x.AnalyticalLayer.LayerName).FirstOrDefault() ?? string.Empty,
                        Purpose = g.Select(x => x.AnalyticalLayer.Purpose).FirstOrDefault() ?? string.Empty,
                        CalText1 = g.Select(x => x.AnalyticalLayer.CalText1).FirstOrDefault(),
                        CalText2 = g.Select(x => x.AnalyticalLayer.CalText2).FirstOrDefault(),
                        CalText3 = g.Select(x => x.AnalyticalLayer.CalText3).FirstOrDefault(),
                        CalText4 = g.Select(x => x.AnalyticalLayer.CalText4).FirstOrDefault(),
                        CalText5 = g.Select(x => x.AnalyticalLayer.CalText5).FirstOrDefault(),
                        Definition = g.Select(x => x.AnalyticalLayer.Definition).FirstOrDefault(),

                        FiveLevelInterpretations = g.First().AnalyticalLayer.FiveLevelInterpretations,

                        cities = g.Select(x => new MutipleCitieskpiLayerResults
                        {
                            CityID = x.CityID,
                            InterpretationID = x.InterpretationID,
                            NormalizeValue = x.NormalizeValue,
                            CalValue1 = x.CalValue1,
                            CalValue2 = x.CalValue2,
                            CalValue3 = x.CalValue3,
                            CalValue4 = x.CalValue4,
                            CalValue5 = x.CalValue5,
                            LastUpdated = x.LastUpdated,

                            AiInterpretationID = x.AiInterpretationID,
                            AiNormalizeValue = x.AiNormalizeValue,
                            AiCalValue1 = x.AiCalValue1,
                            AiCalValue2 = x.AiCalValue2,
                            AiCalValue3 = x.AiCalValue3,
                            AiCalValue4 = x.AiCalValue4,
                            AiCalValue5 = x.AiCalValue5,

                            AiLastUpdated = x.AiLastUpdated,
                            City = x.City
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                    .Success(response ?? new GetMutiplekpiLayerResultsDto());
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetMutiplekpiLayerResults", ex);

                return ResultResponseDto<GetMutiplekpiLayerResultsDto>
                    .Failure(new List<string> { "An error occurred." });
            }
        }
        
        public async Task<Tuple<string, byte[]>> ExportCompareCities(CompareKpiCityRequest c, int userId, UserRole role)
        {
            try
            {
                var payload = new CompareCityRequestDto
                {
                    Cities = c.Cities,  
                    UpdatedAt = c.UpdatedAt
                };

                var result = await CompareCities(payload, userId, role,false);
                var data = result.Result;

                if (data == null || data.TableData == null || !data.TableData.Any())
                {
                    return new Tuple<string, byte[]>("City_Kpis_Comparison.xlsx", Array.Empty<byte>());
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("City Comparison");

                    // =========================
                    // 📊 DYNAMIC HEADER SETUP
                    // =========================
                    var cities = data.TableData.First().CityValues;
                    int totalCols = 2 + (cities.Count * 2);

                    // =========================
                    // 🎯 REPORT HEADER (TOP)
                    // =========================
                    ws.Range(1, 1, 1, totalCols).Merge().Value = "Key Performance Indicator Report";
                    ws.Range(2, 1, 2, totalCols).Merge().Value = $"Report Year: {DateTime.Now.Year}";
                    ws.Range(3, 1, 3, totalCols).Merge().Value = $"Generated On: {DateTime.Now:dd-MMM-yyyy HH:mm}";

                    var titleRange = ws.Range(1, 1, 3, totalCols);
                    titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    titleRange.Style.Font.FontColor = XLColor.White;
                    titleRange.Style.Font.Bold = true;
                    titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Row(1).Height = 28;
                    ws.Row(2).Height = 22;
                    ws.Row(3).Height = 22;

                    // =========================
                    // 📊 MULTI-ROW TABLE HEADER
                    // =========================
                    int row = 5;
                    int col = 1;

                    // KPI Name
                    ws.Range(row, col, row + 1, col).Merge().Value = "KPI Name";
                    col++;

                    // Purpose
                    ws.Range(row, col, row + 1, col).Merge().Value = "Purpose";
                    col++;

                    // Dynamic Cities
                    foreach (var city in cities)
                    {
                        int startCol = col;

                        // City Name (merged)
                        ws.Range(row, startCol, row, startCol + 1).Merge().Value = city.CityName;

                        // Sub headers
                        ws.Cell(row + 1, startCol).Value = "Evaluation";
                        ws.Cell(row + 1, startCol + 1).Value = "AI";

                        col += 2;
                    }

                    // Style header (both rows)
                    var headerRange = ws.Range(row, 1, row + 1, totalCols);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // =========================
                    // 📄 DATA ROWS
                    // =========================
                    row += 2;
                    int startDataRow = row;

                    foreach (var kpi in data.TableData)
                    {
                        col = 1;

                        ws.Cell(row, col++).Value = $"{kpi.LayerName} ({kpi.LayerCode})";

                        var cleanPurpose = kpi.Definition ??"";
                        var purposeCell = ws.Cell(row, col++);
                        purposeCell.Value = string.IsNullOrEmpty(cleanPurpose) ? "NA" : cleanPurpose;

                        if (!string.IsNullOrEmpty(cleanPurpose))
                        {
                            var comment = purposeCell.GetComment();
                            comment.AddText(cleanPurpose);
                            comment.Visible = false;
                        }

                        foreach (var city in kpi.CityValues)
                        {
                            ws.Cell(row, col++).Value = city.Value;
                            ws.Cell(row, col++).Value = city.AiValue;
                        }

                        row++;
                    }

                    int endDataRow = row - 1;

                    // =========================
                    // 🎨 STYLING
                    // =========================

                    // Column widths
                    ws.Column(1).Width = 70;
                    ws.Column(2).Width = 55;

                    for (int i = 3; i <= totalCols; i++)
                    {
                        ws.Column(i).Width = 18;
                    }

                    // Wrap text
                    ws.Column(2).Style.Alignment.WrapText = true;
                    ws.Column(2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                    // Center numbers
                    ws.Columns(3, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Auto height
                    ws.Rows().AdjustToContents();

                    // Freeze (after 2 header rows)
                    ws.SheetView.FreezeRows(6);

                    // Borders
                    var dataRange = ws.Range(5, 1, endDataRow, totalCols);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    // Zebra rows
                    for (int i = startDataRow; i <= endDataRow; i++)
                    {
                        if (i % 2 == 0)
                        {
                            ws.Range(i, 1, i, totalCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                        }
                    }

                    // Auto filter (second header row)
                    ws.Range(6, 1, 6, totalCols).SetAutoFilter();

                    // =========================
                    // 📄 SHEET 2
                    // =========================
                    var ws2 = workbook.Worksheets.Add("KPI Details");

                    int r = 1;

                    ws2.Cell(r, 1).Value = "KPI Name";
                    ws2.Cell(r, 2).Value = "Full Purpose";

                    var header2 = ws2.Range(r, 1, r, 2);
                    header2.Style.Font.Bold = true;
                    header2.Style.Font.FontColor = XLColor.White;
                    header2.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F7D6D");

                    r++;

                    foreach (var kpi in data.TableData)
                    {
                        ws2.Cell(r, 1).Value = $"{kpi.LayerName} ({kpi.LayerCode})";
                        ws2.Cell(r, 2).Value = kpi.Definition ?? "";
                        r++;
                    }

                    ws2.Column(1).Width = 40;
                    ws2.Column(2).Width = 100;
                    ws2.Column(2).Style.Alignment.WrapText = true;

                    ws2.Rows().AdjustToContents();
                    ws2.SheetView.FreezeRows(1);

                    // =========================
                    // 📤 EXPORT
                    // =========================
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return new Tuple<string, byte[]>("City_Comparison.xlsx", stream.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ExportCompareCities", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
    }
}
