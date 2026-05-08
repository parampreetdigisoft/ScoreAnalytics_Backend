namespace PeaceEnablers.Dtos.chatDto
{
    public class CityChatRequestDto
    {
        public int CityID { get; set; }
        public int? PillarID { get; set; }
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
}
