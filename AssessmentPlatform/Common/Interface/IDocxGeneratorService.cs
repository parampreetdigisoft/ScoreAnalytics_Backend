using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Common.Interface
{
    /// <summary>
    /// Low-level Word document generation contract.
    /// Consumed by <see cref="DocumentGeneratorService"/>;
    /// controllers should depend on <see cref="IDocumentGeneratorService"/> instead.
    /// </summary>
    public interface IDocxGeneratorService
    {
        Task<byte[]> GenerateCityDetailsDocx(
            AiCitySummeryDto city,
            List<AiCityPillarReponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCities,
            UserRole userRole);

        Task<byte[]> GeneratePillarDetailsDocx(
            AiCityPillarReponse pillarData,
            UserRole userRole);

        Task<byte[]> GenerateAllCitiesDetailsDocx(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarReponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole);
    }
}
