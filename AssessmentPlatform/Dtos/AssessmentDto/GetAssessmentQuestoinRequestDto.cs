using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetAssessmentQuestoinRequestDto : PaginationRequest
    {
        public int AssessmentID { get; set; } 
        public int? PillarID { get; set; }
    }
}
