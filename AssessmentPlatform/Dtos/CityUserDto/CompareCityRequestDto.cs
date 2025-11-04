namespace AssessmentPlatform.Dtos.CityUserDto
{
    public class CompareCityRequestDto
    {
        public List<int> Cities { get; set; } 
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    }
}
