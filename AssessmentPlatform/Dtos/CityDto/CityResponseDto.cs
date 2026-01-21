using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.CityDto
{
    public class CityResponseDto : City
    {
        public string? AssignedBy { get; set; }
        public decimal? Score { get; set; }// highest score have top rank
        public decimal? AiScore { get; set; }
    }
    public class UserCityMappingResponseDto : CityResponseDto
    {
        public int UserCityMappingID { get; set; }
        public double? Distance { get; set; }
    }
}
