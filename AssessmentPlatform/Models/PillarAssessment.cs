namespace AssessmentPlatform.Models
{
    public class PillarAssessment
    {
        public int PillarAssessmentID { get; set; }
        public int AssessmentID { get; set; }
        public int PillarID { get; set; }
        public Assessment Assessment { get; set; }
        public ICollection<AssessmentResponse> Responses { get; set; } = new List<AssessmentResponse>();
    }
}
