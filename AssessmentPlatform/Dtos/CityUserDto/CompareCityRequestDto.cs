using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.Dtos.CityUserDto
{
    public class CompareCityRequestDto : PaginationRequest
    {
        public List<int> Cities { get; set; }
        public List<int> Kpis { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }

}
