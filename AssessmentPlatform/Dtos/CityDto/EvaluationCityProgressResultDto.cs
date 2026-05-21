namespace AssessmentPlatform.Dtos.CityDto
{
    public class EvaluationCityProgressResultDto
    {
        public int PillarID { get; set; }
        public double Weight { get; set; }
        public bool Reliability { get; set; }
        public int CityID { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public decimal AIProgress { get; set; }
        public decimal NormalizedValue { get; set; }
        public int TotalAssessments { get; set; }
        public int UserID { get; set; }
    }
    public class EvaluationCityProgressHistoryResultDto
    {
        public int PillarID { get; set; }
        public double Weight { get; set; }
        public bool Reliability { get; set; }
        public int CityID { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal ScoreProgress { get; set; }
        public int Year { get; set; }
        public decimal NormalizedValue { get; set; }
        public int TotalAssessments { get; set; }
        public int UserID { get; set; }
    }

    public class CityRankingResultDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public string Region { get; set; }
        public string State { get; set; }
        public int TotalCity { get; set; }
        public int CityRank { get; set; }
        public int TotalCityInCountry { get; set; }
        public int CountryRank { get; set; }
        public decimal CityAIScore { get; set; }
        public int? DataYear { get; set; }
    }

}
