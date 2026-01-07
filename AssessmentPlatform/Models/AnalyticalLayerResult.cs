namespace AssessmentPlatform.Models
{
    public class AnalyticalLayerResult
    {
        public int LayerResultID { get; set; }
        public int LayerID { get; set; }
        public int CityID { get; set; }
        public int? InterpretationID { get; set; }
        public decimal? NormalizeValue { get; set; }
        public decimal? CalValue1 { get; set; }
        public decimal? CalValue2 { get; set; }
        public decimal? CalValue3 { get; set; }
        public decimal? CalValue4 { get; set; }
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int? AiInterpretationID { get; set; }
        public decimal? AiNormalizeValue { get; set; }
        public decimal? AiCalValue1 { get; set; }
        public decimal? AiCalValue2 { get; set; }
        public decimal? AiCalValue3 { get; set; }
        public decimal? AiCalValue4 { get; set; }
        public decimal? AiCalValue5 { get; set; }
        public DateTime? AiLastUpdated { get; set; }
        public AnalyticalLayer AnalyticalLayer { get; set; } = new();
        public City? City { get; set; }
    }
}
