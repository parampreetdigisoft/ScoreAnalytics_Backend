namespace AssessmentPlatform.Dtos.CityDto
{
    public class AddUpdateCityDto
    {
        public int CityID { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string PostalCode { get; set; }
        public string? Region { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }
    }
    public class BulkAddCityDto
    {
        public List<AddUpdateCityDto> Cities { get; set; }
    }
}
