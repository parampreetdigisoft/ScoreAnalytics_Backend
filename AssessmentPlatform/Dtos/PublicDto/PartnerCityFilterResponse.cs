namespace AssessmentPlatform.Dtos.PublicDto
{
    public class PartnerCityFilterResponse
    {
        public List<string> Countries { get; set; }
        public List<string> Regions { get; set; }
        public List<PartnerCityDto> Cities { get; set; }
    }

    public class PartnerCityDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
    }
}
