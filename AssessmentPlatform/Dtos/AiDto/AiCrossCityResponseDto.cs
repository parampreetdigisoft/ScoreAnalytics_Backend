namespace AssessmentPlatform.Dtos.AiDto
{
    public class AiCrossCityResponseDto
    {
        public List<string> Categories { get; set; } = new List<string>();
        public List<ChartSeriesDto> Series { get; set; } = new List<ChartSeriesDto>();
        public List<ChartTableRowDto> TableData { get; set; } = new List<ChartTableRowDto>();
    }

    public class ChartSeriesDto 
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; }
    }

    public class ChartTableRowDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public decimal Value { get; set; }
        public List<PillarValueDto> PillarValues { get; set; } = new List<PillarValueDto>();
    }

    public class PillarValueDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public decimal Value { get; set; }
        public bool IsAccess { get; set; }
    }
}
