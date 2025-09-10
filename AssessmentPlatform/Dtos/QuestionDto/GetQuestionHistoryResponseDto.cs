using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class GetQuestionHistoryResponseDto
    {
        public int QuestionID { get; set; }
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
    }
    public class QuestionsByUserPillarsResponsetDto : GetQuestionHistoryResponseDto
    {
        public List<QuestionsByUserInfo> Users { get; set; } = new List<QuestionsByUserInfo>();
    }

    public class QuestionsByUserInfo
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public int? Score { get; set; }
        public string OptionText { get; set; }
        public string Justification { get; set; }
    }
}
