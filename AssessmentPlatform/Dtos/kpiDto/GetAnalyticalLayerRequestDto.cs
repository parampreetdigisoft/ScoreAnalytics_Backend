using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.kpiDto
{
    public class GetAnalyticalLayerRequestDto : PaginationRequest
    {
        public int? CityID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }
}
