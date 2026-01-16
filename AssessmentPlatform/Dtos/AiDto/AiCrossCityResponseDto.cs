namespace AssessmentPlatform.Dtos.AiDto
{
    public class AiCrossCityResponseDto
    {
        public List<string> Categories { get; set; } = new List<string>();
        public List<CrossCityChartSeriesDto> Series { get; set; } = new List<CrossCityChartSeriesDto>();
        public List<CrossCityChartTableRowDto> TableData { get; set; } = new List<CrossCityChartTableRowDto>();
    }

    public class CrossCityChartSeriesDto
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
    }

    public class CrossCityChartTableRowDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public decimal Value { get; set; }
        public List<CrossCityPillarValueDto> PillarValues { get; set; } = new List<CrossCityPillarValueDto>();
    }

    public class CrossCityPillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal Value { get; set; }
        public bool IsAccess { get; set; }
    }
}
