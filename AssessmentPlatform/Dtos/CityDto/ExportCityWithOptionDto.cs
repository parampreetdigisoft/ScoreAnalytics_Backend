namespace AssessmentPlatform.Dtos.CityDto
{
    public class ExportCityWithOptionDto
    {
        public bool? IsRanking { get; set; }
        public bool? IsAllCity { get; set; }
        public bool? IsPillarLevel { get; set; }
        public List<int>? CityIDs { get; set; }
    }
}
