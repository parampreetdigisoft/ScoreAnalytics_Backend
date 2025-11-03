using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.kpiDto
{
    public class GetAnalyticalLayerResultDto
    {
        public int LayerResultID { get; set; }
        public int LayerID { get; set; }
        public int CityID { get; set; }
        public int? InterpretationID { get; set; }
        public decimal? NormalizeValue { get; set; }
        public decimal? CalValue1 { get; set; }
        public decimal? CalValue2 { get; set; }
        public decimal? CalValue3 { get; set; }
        public decimal? CalValue4 { get; set; }
        public decimal? CalValue5 { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string LayerCode { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? CalText1 { get; set; }
        public string? CalText2 { get; set; }
        public string? CalText3 { get; set; }
        public string? CalText4 { get; set; }
        public string? CalText5 { get; set; }
        public ICollection<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = new List<FiveLevelInterpretation>();
        public City? City { get; set; }
    }
}
