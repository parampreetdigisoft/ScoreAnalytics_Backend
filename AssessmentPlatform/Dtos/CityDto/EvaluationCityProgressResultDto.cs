namespace AssessmentPlatform.Dtos.CityDto
{
    public class EvaluationCityProgressResultDto
    {
        public int PillarID { get; set; }
        public string? PillarName { get; set; }
        public double Weight { get; set; }
        public bool Reliability { get; set; }
        public int CityID { get; set; }
        public string? CityName { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public decimal AIProgress { get; set; }
        public decimal ScoreDiff { get; set; }
        public decimal NormalizedValue { get; set; }
        public int UserID { get; set; }
    }
}
