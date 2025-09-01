namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetCityQuestionHistoryReponseDto
    {
        public int CityID { get; set; }
        public int TotalAssessment { get; set; }
        public decimal Score { get; set; }
        public int TotalPillar { get; set; }
        public int TotalAnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int AnsQuestion { get; set; }
        public List<CityPillarQuestionHistoryReponseDto> Pillars { get; set; } = new();
    }

    public class CityPillarQuestionHistoryReponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public decimal Score { get; set; }
        public int AnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int AnsQuestion { get; set; }
    }
}
