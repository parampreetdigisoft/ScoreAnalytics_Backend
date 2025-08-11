using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class GetQuestionRequestDto : PaginationRequest
    {
        public int? PillarID { get; set; }
        public int? CityID { get; set; }
    }
}
