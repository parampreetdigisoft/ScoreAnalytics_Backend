namespace AssessmentPlatform.Dtos.chatDto
{
    public class CityChatRequestDto : ChatGlobalAskQuestionRequestDto
    {
        public int CityID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequestDto
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
}
