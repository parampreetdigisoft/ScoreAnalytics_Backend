namespace AssessmentPlatform.Dtos.AiDto
{
    public class DownloadReportDto
    {
        public List<int> CityIDs { get; set; }
        public Common.Interface.DocumentFormat Format { get; set; } = Common.Interface.DocumentFormat.Pdf;

    }
}
