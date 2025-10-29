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
        public string? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public AnalyticalLayer AnalyticalLayer { get; set; } = new();
        public City? City { get; set; }
    }
}
