namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetCityPillarHistoryRequestDto
    {
        public int UserID { get; set; }
        public int CityID { get; set; }
        public int? PillarID { get; set; }
    }
}
