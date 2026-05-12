namespace AssessmentPlatform.Models
{
    public class AIAssistantFAQ
    {
        public int FAQID { get; set; }
        public string Related { get; set; }
        public string Category { get; set; }
        public string QuestionText { get; set; }
        public string? AnswerText { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string ProcName { get; set; }
    }
}
