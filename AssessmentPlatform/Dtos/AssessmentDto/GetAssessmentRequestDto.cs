using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;
using DocumentFormat.OpenXml.Bibliography;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetAssessmentRequestDto : PaginationRequest
    {
        public int? SubUserID { get; set; } //Means admin or analyst can see result of a user that they has permission
        public int? CityID { get; set; }
        public UserRole? Role { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }
}
    