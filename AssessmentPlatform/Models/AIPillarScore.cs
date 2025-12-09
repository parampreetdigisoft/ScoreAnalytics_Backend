namespace AssessmentPlatform.Models
{
    public class AIPillarScore
    {
        public int PillarScoreID { get; set; }
        public int CityID { get; set; }
        public int PillarID { get; set; }
        public int Year { get; set; }

        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorScore { get; set; }
        public decimal? Discrepancy { get; set; }

        public string ConfidenceLevel { get; set; }
        public string EvidenceSummary { get; set; }
        public string RedFlags { get; set; }
        public string GeographicEquityNote { get; set; }
        public string InstitutionalAssessment { get; set; }
        public string DataGapAnalysis { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public City? City { get; set; }
        public Pillar? Pillar { get; set; }
        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }

}
