namespace AssessmentPlatform.Models
{
    public class PublicUserCityMapping
    {
        public int PublicUserCityMappingID { get; set; }
        public int UserID { get; set; }
        public int CityID { get; set; }
        public City? City { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
