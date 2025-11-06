namespace AssessmentPlatform.Dtos.CityDto
{
    public class CityHistoryDto
    {
        public int TotalCity { get; set; }
        public int TotalAnalyst { get; set; }
        public int TotalEvaluator { get; set; }
        public int ActiveCity { get; set; }
        public int TotalAccessCity { get; set; }
        public int CompeleteCity { get; set; }
        public int InprocessCity { get; set; }
        public decimal AvgHighScore { get; set; }
        public decimal AvgLowerScore { get; set; }
        public decimal OverallVitalityScore { get; set; }
    }
}
