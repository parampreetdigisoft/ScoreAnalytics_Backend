namespace AssessmentPlatform.Dtos.kpiDto
{
    public class CompareCityResponseDto
    {
        public List<string> Categories { get; set; }
        public List<ChartSeriesDto> Series { get; set; }
        public List<ChartTableRowDto> TableData { get; set; }
    }

    public class ChartSeriesDto
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
        public List<decimal> AiData { get; set; }
    }

    public class ChartTableRowDto
    {
        public string LayerCode { get; set; }
        public string LayerName { get; set; }
        public List<CityValueDto> CityValues { get; set; }
        public decimal PeerCityScore { get; set; }
    }

    public class CityValueDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public decimal Value { get; set; }
        public decimal AiValue { get; set; }
    }
}
