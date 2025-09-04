namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetCityQuestionHistoryReponseDto : GetCitySubmitionHistoryReponseDto
    {
        public List<CityPillarQuestionHistoryReponseDto> Pillars { get; set; } = new();
    }
    public class GetCitySubmitionHistoryReponseDto
    {
        public int CityID { get; set; }
        public int TotalAssessment { get; set; }
        public decimal Score { get; set; }
        public decimal ScoreProgress { get; set; }
        public int TotalPillar { get; set; }
        public int TotalAnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int AnsQuestion { get; set; }
    }
    public class GetCitiesSubmitionHistoryReponseDto : GetCitySubmitionHistoryReponseDto
    {
        public string CityName { get; set; }
    }

    public class CityPillarQuestionHistoryReponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public decimal Score { get; set; }
        public decimal ScoreProgress { get; set; }
        public int AnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int AnsQuestion { get; set; }
    }
    public class GetAssessmentHistoryDto
    {
        public int AssessmentID { get; set; }
        public double Score { get; set; }
        public int TotalAnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int TotalAnsQuestion { get; set; }
        public double CurrentProgress { get; set; }
    }
}
