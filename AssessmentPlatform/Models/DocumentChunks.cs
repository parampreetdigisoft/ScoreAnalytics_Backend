namespace AssessmentPlatform.Models
{
    public class DocumentChunks
    {
        public string ChunkID { get; set; } = string.Empty;

        public int CityDocumentID { get; set; }

        public int TOCID { get; set; }

        public int? CityID { get; set; }

        public int? PillarID { get; set; }

        public int ChunkIndex { get; set; }

        public string? ChunkText { get; set; }

        public int? TokenCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
