using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.AiDto
{
    public class AiCitySummeryRequestDto : PaginationRequest
    {
        public int? CityID { get; set; }
    }

    public class AiCityPillarSummeryRequestDto : AiCitySummeryRequestDto
    {
        public int? PillarID { get; set; }
    }

}
