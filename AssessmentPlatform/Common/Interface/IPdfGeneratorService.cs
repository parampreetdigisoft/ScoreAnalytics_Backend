using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Common.Interface
{
    public interface IPdfGeneratorService
    {

        Task<byte[]> GenerateCityDetailsPdf(AiCitySummeryDto city, List<AiCityPillarReponse> pillars, List<KpiChartItem> kpis, List<PeerCityHistoryReportDto> peerCity, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiCityPillarReponse cityDetails, UserRole userRole);
        Task<byte[]> GenerateAllCitiesDetailsPdf(List<AiCitySummeryDto> cities, Dictionary<int, List<AiCityPillarReponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
