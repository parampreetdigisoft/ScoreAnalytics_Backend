namespace AssessmentPlatform.Dtos.CityDto
{
    public class GetNearestCityRequestDto
    {
        public int UserID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
