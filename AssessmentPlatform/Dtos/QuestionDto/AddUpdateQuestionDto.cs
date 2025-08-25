using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class AddUpdateQuestionDto
    {
        public int QuestionID { get; set; } = 0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public List<QuestionOption> QuestionOptions { get; set; }
    }
    public class AddBulkQuestionsDto
    {
        public List<AddUpdateQuestionDto> Questions { get; set; }
    }
}
