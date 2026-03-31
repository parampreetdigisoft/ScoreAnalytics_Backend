namespace AssessmentPlatform.Dtos.CityDto
{
    public class AddUpdateCityDto
    {
        public int CityID { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string? CityAliasName { get; set; }
        public string PostalCode { get; set; }
        public string? Region { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int? Population { get; set; }
        public decimal? Income { get; set; }
        public decimal? LivingCost { get; set; }
        public decimal? PurchasingPower { get; set; }
        public List<int>? PeerCities { get; set; }
    }
    public class BulkAddCityDto
    {
        public List<AddUpdateCityDto> Cities { get; set; }
    }
}
