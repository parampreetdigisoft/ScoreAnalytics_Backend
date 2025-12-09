namespace AssessmentPlatform.Models
{
    public class AIEstimatedQuestionScore
    {
        public int QuestionScoreID { get; set; }
        public int CityID { get; set; }
        public int PillarID { get; set; }
        public int QuestionID { get; set; }
        public int Year { get; set; }

        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }

        public string ConfidenceLevel { get; set; }
        public int? DataSourcesUsed { get; set; }

        public string EvidenceSummary { get; set; }
        public string RedFlags { get; set; }
        public string GeographicEquityNote { get; set; }

        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public string SourceURL { get; set; }
        public int? SourceDataYear { get; set; }
        public string SourceDataExtract { get; set; }
        public int? SourceTrustLevel { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public City? City { get; set; }
        public Pillar? Pillar { get; set; }
        public Question? Question { get; set; }
    }

}
