namespace AssessmentPlatform.Models
{
    public class AssistantChatHistory
    {
        public int ChatID { get; set; }
        public int? CityID { get; set; }
        public int? PillarID { get; set; }
        public int? QuestionID { get; set; }
        public int UserID { get; set; }
        public string SessionToken { get; set; }
        public string QuestionText { get; set; }
        public string? AnswerText { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
