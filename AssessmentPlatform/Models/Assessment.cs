namespace AssessmentPlatform.Models
{
    public class Assessment
    {
        public int AssessmentID { get; set; }
        public int UserCityMappingID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public UserCityMapping UserCityMapping { get; set; }
        public ICollection<AssessmentResponse> Responses { get; set; }
    }
}
