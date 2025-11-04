namespace AssessmentPlatform.Dtos.kpiDto
{
    public class CompareCityResponseDto
    {
        public List<CompareCitiesDto> Cities { get; set; }
    }
    public class CompareCitiesDto
    {
         public int CityID { get; set; }
         public string CityName { get; set; }
        public List<GetAnalyticalLayerSimpleResultDto> Kpis { get; set; }
    }
}
