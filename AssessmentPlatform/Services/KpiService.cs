using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
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
        public async Task<PaginationResponse<GetAnalyticalLayerResultDto>> 
            GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending)
        {
            try
            {
                var year = request.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);

                IQueryable<GetAnalyticalLayerResultDto> query;

                if (role == UserRole.CityUser)
                {
                    var validCities = _context.PublicUserCityMappings
                        .Where(x =>
                            (!request.CityID.HasValue || x.CityID == request.CityID) &&
                            x.IsActive &&
                            x.UserID == userId)
                        .Select(x => x.CityID);

                    var validPillarIds = _context.CityUserPillarMappings
                    .Where(x => x.IsActive && x.UserID == userId)
                    .Select(x => x.PillarID);


                    // Step 1: Get valid KPI IDs for this user
                    var validKpis = _context.AnalyticalLayerPillarMappings
                        .Where(x => validPillarIds.Contains(x.PillarID))
                        .Select(x => x.LayerID)
                        .Distinct();



                    query =
                        from ar in _context.AnalyticalLayerResults
                            .Include(ar => ar.AnalyticalLayer)
                                .ThenInclude(al => al.FiveLevelInterpretations)
                            .Include(ar => ar.City)
                        join vc in validCities on ar.CityID equals vc
                        join vk in validKpis on ar.LayerID equals vk
                        where (ar.LastUpdated >= startDate && ar.LastUpdated < endDate) || (ar.AiLastUpdated >= startDate && ar.AiLastUpdated < endDate)
                        select new GetAnalyticalLayerResultDto
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
                            FiveLevelInterpretations = ar.AnalyticalLayer.FiveLevelInterpretations,

                            City = ar.City
                        };
                }
                else
                {
                    Expression<Func<AnalyticalLayerResult, bool>> expression = x =>
                       (!request.CityID.HasValue || x.CityID == request.CityID)
                       && (!request.LayerID.HasValue || x.LayerID == request.LayerID)
                       && (x.LastUpdated >= startDate && x.LastUpdated < endDate) || (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate);

                    query = _context.AnalyticalLayerResults
                        .Include(ar => ar.AnalyticalLayer)
                            .ThenInclude(al => al.FiveLevelInterpretations)
                        .Include(ar => ar.City)
                        .Where(expression)
                        .Select(ar => new GetAnalyticalLayerResultDto
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
                            FiveLevelInterpretations = ar.AnalyticalLayer.FiveLevelInterpretations,

                            City = ar.City
                        });
                }

                var response = await query.ApplyPaginationAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAnalyticalLayers", ex);
                return new PaginationResponse<GetAnalyticalLayerResultDto>();
            }
        }
        public async Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi()
        {
            try
            {
                var result = await _context.AnalyticalLayers
                    .Where(ar => !ar.IsDeleted)
                    .ToListAsync();
                    
                 return ResultResponseDto<List<AnalyticalLayer>>.Success(result); 
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAnalyticalLayers", ex);
                return  ResultResponseDto<List<AnalyticalLayer>>.Failure(new List<string> { "an error occure"});
            }
        }
        public async Task<ResultResponseDto<CompareCityResponseDto>> CompareCities(CompareCityRequestDto c, int userId, UserRole role)
        {
            try
            {
                var year = c.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);


                var validKpiIds = new List<int>();
                if (c.Kpis.Count == 0)
                {
                    var query = _context.AnalyticalLayers
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.LayerID)
                    .OrderBy(x => x);

                    var res = await query.ApplyPaginationAsync(c);
                    validKpiIds = res.Data.ToList() ;
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

                if(role == UserRole.Analyst || role == UserRole.Evaluator)
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
                    && (x.AiLastUpdated >= startDate && x.AiLastUpdated < endDate || x.LastUpdated >= startDate && x.LastUpdated < endDate) 
                    && validKpiIds.Contains(x.LayerID))
                    .Select(ar => new
                    {
                        ar.CityID,
                        ar.LayerID,
                        ar.AnalyticalLayer.LayerCode,
                        ar.AnalyticalLayer.LayerName,
                        ar.CalValue5,
                        ar.AiCalValue5
                    })
                    .ToListAsync();

                // Step 4: Get all distinct layers
                var allLayers = analyticalResults
                    .Select(x => new { x.LayerID, x.LayerCode, x.LayerName })
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
                        LayerCode = layer.LayerCode,
                        LayerName = layer.LayerName,
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
    }
}
