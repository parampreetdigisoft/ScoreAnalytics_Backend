namespace PeaceEnablers.Dtos.chatDto
{
    public class ChatResponseDto
    {
        public int CityID { get; set; }
        public int? PillarID { get; set; }
        public string QuestionText { get; set; }
        public int? FAQID { get; set; }
        public string ResponseText { get; set; }
    }
}
