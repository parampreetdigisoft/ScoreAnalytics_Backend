namespace AssessmentPlatform.Dtos.CityUserDto
{
    public class CityPillarQuestionDetailsDto
    {
        public int QuestionID { get; set; } = 0;
        public string QuestionText { get; set; }
        public decimal TotalScore { get; set; } = 0;
        public decimal ScoreProgress { get; set; } = 0;
        public int TotalQuestion { get; set; } = 0;
        public int AnsQuestion { get; set; } = 0;
        public decimal AvgHighScore { get; set; } = 0;
        public decimal AvgLowerScore { get; set; } = 0;
        public int TotalUnKnown { get; set; } = 0;
        public int TotalNA { get; set; } = 0;
    }
}
