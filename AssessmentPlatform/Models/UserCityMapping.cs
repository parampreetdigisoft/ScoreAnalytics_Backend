namespace AssessmentPlatform.Models
{
    public class UserCityMapping
    {
        public int UserCityMappingID { get; set; }
        public int UserID { get; set; }
        public UserRole Role { get; set; }
        public int CityID { get; set; }
        public int AssignedByUserId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
