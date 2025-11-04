namespace AssessmentPlatform.Dtos.kpiDto
{
    public class GetAnalyticalLayerSimpleResultDto
    {
        public int LayerResultID { get; set; }
        public int LayerID { get; set; }
        public int? InterpretationID { get; set; }
        public string Condition { get; set; }
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string? CalText5 { get; set; }
    }
}

