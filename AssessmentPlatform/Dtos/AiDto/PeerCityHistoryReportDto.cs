namespace AssessmentPlatform.Dtos.AiDto
{
    public class PeerCityHistoryReportDto
    {
        public int CityID { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string? PostalCode { get; set; }
        public string? Region { get; set; }
        public string Country { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string? Image { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Population { get; set; }
        public decimal? Income { get; set; }
        public List<PeerCityYearHistoryDto> CityHistory { get; set; }
    }

    public class PeerCityYearHistoryDto
    {
        public int CityID { get; set; }
        public int Year { get; set; } = 0;
        public decimal ScoreProgress { get; set; }
        public List<PeerCityPillarHistoryReportDto> Pillars { get; set; }
    }

    public class PeerCityPillarHistoryReportDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public decimal ScoreProgress { get; set; }

    }
}
