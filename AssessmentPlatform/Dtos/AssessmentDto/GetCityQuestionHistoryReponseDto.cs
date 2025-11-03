namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetCityQuestionHistoryReponseDto : GetCitySubmitionHistoryReponseDto
    {
        public List<CityPillarQuestionHistoryReponseDto> Pillars { get; set; } = new();
    }
    public class GetCitySubmitionHistoryReponseDto
    {
        public int CityID { get; set; }
        public int TotalAssessment { get; set; } = 0;
        public decimal Score { get; set; } = 0;
        public decimal ScoreProgress { get; set; } = 0;
        public int TotalPillar { get; set; } = 0;
        public int TotalAnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
    }
    public class GetCitiesSubmitionHistoryReponseDto : GetCitySubmitionHistoryReponseDto
    {
        public string CityName { get; set; }
    }

    public class CityPillarQuestionHistoryReponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public bool IsAccess { get; set; } = false;
        public decimal Score { get; set; }=0;
        public decimal ScoreProgress { get; set; } = 0;
        public int AnsPillar { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
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

    public class CityPillarUserHistoryReponseDto : CityPillarQuestionHistoryReponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
    }
}
