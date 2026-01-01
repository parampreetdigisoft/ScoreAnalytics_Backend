namespace AssessmentPlatform.Models
{
    public class AnalyticalLayerPillarMapping
    {
        public int AnalyticalLayerPillarMappingID { get; set; }
        public int LayerID { get; set; }
        public int PillarID { get; set; }
        public string Category { get; set; } = string.Empty;
        public int CategoryNumber { get; set; }
    }
}
