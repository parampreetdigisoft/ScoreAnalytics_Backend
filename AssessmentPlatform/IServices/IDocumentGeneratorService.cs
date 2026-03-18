using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Common.Interface
{
    /// <summary>
    /// Output format for document generation.
    /// PDF is the default; Docx produces an editable Word document.
    /// </summary>
    public enum DocumentFormat
    {
        Pdf,
        Docx
    }

    /// <summary>
    /// Unified document-generation service.
    /// Replaces direct calls to IPdfGeneratorService.
    /// Pass <see cref="DocumentFormat.Docx"/> to get a Word document instead of a PDF.
    /// </summary>
    public interface IDocumentGeneratorService
    {
        /// <summary>Full city report: dashboard, summary, pillars, peer comparison, trends, KPI dashboard.</summary>
        Task<byte[]> GenerateCityDetails(
            AiCitySummeryDto city,
            List<AiCityPillarReponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCity,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Single pillar detail report.</summary>
        Task<byte[]> GeneratePillarDetails(
            AiCityPillarReponse pillarData,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Combined report covering every city in the list.</summary>
        Task<byte[]> GenerateAllCitiesDetails(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarReponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);
    }
}
