namespace AssessmentPlatform.Models
{
    public class Pillar
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string Description { get; set; }
        public int DisplayOrder { get; set; }
        public string ImagePath { get; set; }
        public double Weight { get; set; } = 1.0; // Default equal weight
        public bool Reliability { get; set; } = true; // Default fully reliable
        public ICollection<Question> Questions { get; set; }
    }
} 