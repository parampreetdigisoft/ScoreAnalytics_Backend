using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AiDto
{

    public class AiCityPillarReponseDto
    {
        public List<AiCityPillarReponse> Pillars { get; set; }
    }
    public class AiCityPillarReponse
    {
        public int PillarScoreID { get; set; }
        public int CityID { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string Country { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public string ImagePath { get; set; }
        public bool IsAccess { get; set; } = false;
        public int AIDataYear { get; set; }
        public decimal? AIScore { get; set; }
        public decimal? AIProgress { get; set; }
        public decimal? EvaluatorProgress { get; set; }
        public decimal? Discrepancy { get; set; }
        public string ConfidenceLevel { get; set; }
        public string EvidenceSummary { get; set; }
        public string RedFlags { get; set; }
        public string GeographicEquityNote { get; set; }
        public string InstitutionalAssessment { get; set; }
        public string DataGapAnalysis { get; set; }
        public decimal? AICompletionRate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<AIDataSourceCitation>? DataSourceCitations { get; set; }
    }
}
