namespace AssessmentPlatform.Dtos.dashboard
{
    public class AiCityPillarDashboardResponseDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
        public List<CityPillarDashboardPillarValueDto> Pillars { get; set; } = new List<CityPillarDashboardPillarValueDto>();
    }

    public class CityPillarDashboardPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal EvaluationValue { get; set; }
        public decimal AiValue { get; set; }
    }
}
