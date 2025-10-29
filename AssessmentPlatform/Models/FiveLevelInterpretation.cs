namespace AssessmentPlatform.Models
{
    public class FiveLevelInterpretation
    {
        public int InterpretationID { get; set; }
        public int LayerID { get; set; }
        public decimal? MinRange { get; set; }
        public decimal? MaxRange { get; set; } 
        public string Condition { get; set; }
        public string Descriptor { get; set; }
        public string UrbanSignal { get; set; }
        public string StrategicAction { get; set; }
        public AnalyticalLayer? AnalyticalLayer { get; set; }
    }
}
