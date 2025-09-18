using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetCityPillarHistoryRequestDto
    {
        public int UserID { get; set; }
        public int CityID { get; set; }
        public int? PillarID { get; set; }
    }
    public class PillarWithQuestionsDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; }
        public int TotalQuestions { get; set; }
        public List<QuestionWithUserDto> Questions { get; set; } = new();
    }

    public class QuestionWithUserDto
    {
        public int QuestionID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
        public Dictionary<int, QuestionUserAnswerDto> Users { get; set; } = new();
    }

    public class QuestionUserAnswerDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public int? Score { get; set; }
        public string Justification { get; set; }
        public string OptionText { get; set; }
    }
    public class ChangeAssessmentStatusRequestDto
    {
        public int UserID { get; set; }
        public int AssessmentID { get; set; }
        public AssessmentPhase AssessmentPhase { get; set; }
    }
}
