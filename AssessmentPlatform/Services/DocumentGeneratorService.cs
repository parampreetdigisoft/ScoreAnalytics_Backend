using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Common.Implementation
{
    /// <summary>
    /// Facade that delegates to <see cref="PdfGeneratorService"/> or
    /// <see cref="DocxGeneratorService"/> based on the requested <see cref="DocumentFormat"/>.
    ///
    /// Register as: services.AddScoped&lt;IDocumentGeneratorService, DocumentGeneratorService&gt;()
    /// </summary>
    public sealed class DocumentGeneratorService : IDocumentGeneratorService
    {
        private readonly IPdfGeneratorService _pdf;
        private readonly IDocxGeneratorService _docx;

        public DocumentGeneratorService(
            IPdfGeneratorService pdf,
            IDocxGeneratorService docx)
        {
            _pdf = pdf;
            _docx = docx;
        }

        public Task<byte[]> GenerateCityDetails(
            AiCitySummeryDto city,
            List<AiCityPillarReponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCityHistoryReportDto> peerCity,
            UserRole userRole,
        Interface.DocumentFormat format = Interface.DocumentFormat.Pdf)
        {
             var result = format == Interface.DocumentFormat.Docx
                ? _docx.GenerateCityDetailsDocx(city, pillars, kpis, peerCity, userRole)
                : _pdf.GenerateCityDetailsPdf(city, pillars, kpis, peerCity, userRole);

            return result;
        }

        public Task<byte[]> GeneratePillarDetails(
            AiCityPillarReponse pillarData,
            UserRole userRole,
            Interface.DocumentFormat format = Interface.DocumentFormat.Pdf)
            => format == Interface.DocumentFormat.Docx
                ? _docx.GeneratePillarDetailsDocx(pillarData, userRole)
                : _pdf.GeneratePillarDetailsPdf(pillarData, userRole);

        public Task<byte[]> GenerateAllCitiesDetails(
            List<AiCitySummeryDto> cities,
            Dictionary<int, List<AiCityPillarReponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            Interface.DocumentFormat format = Interface.DocumentFormat.Pdf)
            => format == Interface.DocumentFormat.Docx
                ? _docx.GenerateAllCitiesDetailsDocx(cities, pillarsDict, kpis, userRole)
                : _pdf.GenerateAllCitiesDetailsPdf(cities, pillarsDict, kpis, userRole);
    }
}
