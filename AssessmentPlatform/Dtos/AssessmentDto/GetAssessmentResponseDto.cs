using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetAssessmentResponseDto
    {
        public int AssessmentID { get; set; }
        public int UserCityMappingID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int CityID { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public bool IsActive { get; set; } = true;
        public int UserID { get; set; }
        public string UserName { get; set; }
        public decimal Score { get; set; }
        public string AssignedByUser { get; set; }
        public int AssignedByUserId { get; set; }
        public int AssessmentYear { get; set; } 
        public AssessmentPhase? AssessmentPhase { get; set; }
    }

    public class GetCityAssessmentResponseDto : GetAssessmentResponseDto
    {
        public int TotalUnknown { get; set; }
        public int TotalNA { get; set; }
    }
}
