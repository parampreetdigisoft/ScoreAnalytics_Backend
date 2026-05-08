namespace PeaceEnablers.Dtos.chatDto
{
    public class AIAssistantFAQDto
    {
        public int FAQID { get; set; }
        public string Related { get; set; }
        public string Category { get; set; }
        public string QuestionText { get; set; }
        public string? AnswerText { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsAnsweredFaq { get; set; }
    }
}
