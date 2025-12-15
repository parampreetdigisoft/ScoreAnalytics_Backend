using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.AiDto
{
    public class AiCitySummeryRequestDto : PaginationRequest
    {
        public int? CityID { get; set; }
    }
}
