using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class AddAssessmentDto
    {
        public int AssessmentID { get; set; }
        public int UserCityMappingID { get; set; }
        public int PillarID { get; set; }
        public List<AddAssesmentResponseDto> Responses { get; set; }
    }
    public class AddAssesmentResponseDto
    {
        public int AssessmentID { get; set; }
        public int QuestionID { get; set; }
        public int QuestionOptionID { get; set; }
        public ScoreValue? Score { get; set; }
        public string Justification { get; set; }
    }
}
