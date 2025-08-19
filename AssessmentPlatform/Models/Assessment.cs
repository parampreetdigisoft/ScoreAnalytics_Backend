namespace AssessmentPlatform.Models
{
    public class Assessment
    {
        public int AssessmentID { get; set; }
        public int CityID { get; set; }
        public int UserID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<AssessmentResponse> Responses { get; set; }
    }
}
