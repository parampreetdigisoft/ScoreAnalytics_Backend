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

    public class CrossComparisionRequestDto
    {
        public List<int> CityIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }

    public class CrossComparisionRequest
    {
        public List<int> CityIDs { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
    }
}
