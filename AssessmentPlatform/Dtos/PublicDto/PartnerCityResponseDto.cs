

namespace AssessmentPlatform.Dtos.PublicDto
{
    public class PartnerCityResponseDto : PartnerCityHistoryResponseDto
    {
        public int CityID { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string? PostalCode { get; set; }
        public string? Region { get; set; }
        public string? Image { get; set; }
    }

    public class PartnerCityHistoryResponseDto
    {
        public decimal Score { get; set; }
        public decimal HighScore { get; set; }
        public decimal LowerScore { get; set; }
        public decimal Progress { get; set; }
        
    }
}
