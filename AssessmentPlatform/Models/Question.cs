namespace AssessmentPlatform.Models
{
    public class Question
    {
        public int QuestionID { get; set; }
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
        public Pillar Pillar { get; set; }
    }
} 