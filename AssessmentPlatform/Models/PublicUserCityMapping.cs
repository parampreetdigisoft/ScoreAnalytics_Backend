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
    public class CityUserPillarMapping
    {
        public int CityUserPillarMappingID { get; set; }
        public int PillarID { get; set; }
        public int UserID { get; set; }
        public Pillar? City { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
    public class CityUserKpiMapping
    {
        public int CityUserKpiMappingID { get; set; }
        public int LayerID { get; set; }
        public int UserID { get; set; }
        public AnalyticalLayer? Layer { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}
