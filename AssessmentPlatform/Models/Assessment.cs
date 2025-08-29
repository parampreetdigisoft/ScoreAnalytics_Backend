namespace AssessmentPlatform.Models
{
    public class Assessment
    {
        public int AssessmentID { get; set; }
        public int UserCityMappingID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public UserCityMapping UserCityMapping { get; set; }
        public ICollection<PillarAssessment> PillarAssessments { get; set; } = new List<PillarAssessment>();
    }
}
