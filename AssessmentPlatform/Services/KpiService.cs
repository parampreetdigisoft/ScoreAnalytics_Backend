using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;

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
        private bool IsPillarAccess(Enums.TieredAccessPlan tier, int order)
        {
            return tier switch
            {
                Enums.TieredAccessPlan.Basic => order <= 3,
                Enums.TieredAccessPlan.Standard => order <= 7,
                Enums.TieredAccessPlan.Premium => order <= 14,
                _ => false
            };
        }

        public async Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request)
        {
            try
            {
                var query = _context.AnalyticalLayerResults
                    .Include(ar => ar.AnalyticalLayer)
                        .ThenInclude(al => al.FiveLevelInterpretations)
                    .Include(ar => ar.City)
                    .Where(ar => ar.LastUpdated.Year == request.UpdatedAt.Year &&
                                (!request.CityID.HasValue || ar.CityID == request.CityID)
                           )
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

                var response = await query.ApplyPaginationAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error occurred in GetAnalyticalLayers", ex);
                return new PaginationResponse<GetAnalyticalLayerResultDto>();
            }
        }
    }
}
