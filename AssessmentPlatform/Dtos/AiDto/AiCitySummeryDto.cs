namespace AssessmentPlatform.Dtos.AiDto
{
    public class AiCitySummeryDto
    {
        public int CityID { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string Country { get; set; }
        public string? Image { get; set; }
        public int ScoringYear { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorProgress { get; set; }
        public decimal? Discrepancy { get; set; }
        public string ConfidenceLevel { get; set; }
        public string EvidenceSummary { get; set; }
        public string CrossPillarPatterns { get; set; }
        public string InstitutionalCapacity { get; set; }
        public string EquityAssessment { get; set; }
        public string SustainabilityOutlook { get; set; }
        public string StrategicRecommendations { get; set; }
        public string DataTransparencyNote { get; set; }
    }
}
