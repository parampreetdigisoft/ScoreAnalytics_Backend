namespace AssessmentPlatform.Models
{
    public class QuestionOption
    {
        public int OptionID { get; set; }
        public int QuestionID { get; set; }
        public string OptionText { get; set; }
        public int? ScoreValue { get; set; }
        public int? DisplayOrder { get; set; }
        public Question? Question { get; set; }  
    }
}
