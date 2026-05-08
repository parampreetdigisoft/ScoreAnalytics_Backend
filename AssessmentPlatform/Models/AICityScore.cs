namespace AssessmentPlatform.Models
{
    public class AICityScore
    {
        public int CityScoreID { get; set; }
        public int CityID { get; set; }
        public int Year { get; set; }

        public decimal? AIScore { get; set; }// out of 4
        public decimal? AIProgress { get; set; }// out of 100
        public decimal? EvaluatorProgress { get; set; }
        public decimal? Discrepancy { get; set; }

        public string ConfidenceLevel { get; set; }
        public string EvidenceSummary { get; set; }
        public string CrossPillarPatterns { get; set; }
        public string InstitutionalCapacity { get; set; }
        public string EquityAssessment { get; set; }
        public string SustainabilityOutlook { get; set; }
        public string StrategicRecommendations { get; set; }
        public string DataTransparencyNote { get; set; }//WHY THIS ASSESSMENT MATTERS
        public DateTime? UpdatedAt { get; set; }
        public City? City { get; set; }
        public bool IsVerified { get; set; }
        public int? VerifiedBy { get; set; }
        public string? ImmediateSituationSummary { get; set; } // Generates structured summaries (daily, weekly, or on-demand)
        public string? KeyDevelopments { get; set; }
        public string? CriticalRisks { get; set; }
        public string? Gaps { get; set; }
    }

}
