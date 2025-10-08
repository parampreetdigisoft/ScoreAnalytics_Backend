using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.PublicDto
{
    public class PartnerCityRequestDto : PaginationRequest
    {
        public string? Country { get; set; }
        public int? CityID { get; set; }
        public string? Region { get; set; }
        public int? PillarID { get; set; }
    }
    
}
