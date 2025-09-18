using AssessmentPlatform.Models;
using System.Text.Json.Serialization;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class AddUpdateQuestionDto
    {
        public int QuestionID { get; set; } = 0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public bool IsSelected { get; set; } = false;
        public List<QuestionOption> QuestionOptions { get; set; }
    }
    public class AddBulkQuestionsDto
    {
        public List<AddUpdateQuestionDto> Questions { get; set; }
    }

    public class AssessmentQuestionResponseDto
    {
        public int QuestionID { get; set; } = 0;
        public int ResponseID { get; set; } = 0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public bool IsSelected { get; set; } = false;
        public List<QuestionOptionDto> QuestionOptions { get; set; }
    }
    public class QuestionOptionDto
    {
        public int OptionID { get; set; }
        public int QuestionID { get; set; }
        public string OptionText { get; set; }
        public int? ScoreValue { get; set; }
        public int? DisplayOrder { get; set; }
        public bool IsSelected { get; set; } = false;
        public string Justification { get; set; } 
    }
}
