using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetAssessmentQuestionResponseDto
    {
        public int AssessmentID { get; set; }
        public int UserID { get; set; }
        public int PillerID { get; set; }
        public string PillarName { get; set; }
        public int QuestoinID { get; set; }
        public string QuestionText { get; set; }
        public string QuestionOptionText { get; set; }
        public string Justification { get; set; }
        public ScoreValue? Score { get; set; }
    }
}
