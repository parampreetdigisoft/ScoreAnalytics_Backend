namespace AssessmentPlatform.Models
{
    public class AIDataSourceCitation
    {
        public int CitationID { get; set; }

        public int? PillarScoreID { get; set; }

        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public string SourceURL { get; set; }
        public int? DataYear { get; set; }
        public string DataExtract { get; set; }
        public int? TrustLevel { get; set; }
        public DateTime? CreatedAt { get; set; }

        // Navigation
        public AIPillarScore? PillarScore { get; set; }
    }
}
