using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.CityDto
{
    public class CityResponseDto : City
    {
        public string? AssignedBy { get; set; }
    }
    public class UserCityMappingResponseDto : CityResponseDto
    {
        public int UserCityMappingID { get; set; }
    }
}
