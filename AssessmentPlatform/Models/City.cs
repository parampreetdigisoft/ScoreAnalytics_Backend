namespace AssessmentPlatform.Models
{
    public class City
    {
        public int CityID { get; set; }
        public string State { get; set; }       
        public string CityName { get; set; }      
        public string? PostalCode { get; set; }      
        public string? Region { get; set; }
        public bool IsActive { get; set; }  = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string Country { get; set; }
        public string? Image { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
