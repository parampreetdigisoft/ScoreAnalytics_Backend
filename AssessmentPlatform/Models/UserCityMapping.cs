namespace AssessmentPlatform.Models
{
    public class UserCityMapping
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserRole Role { get; set; }
        public int CityId { get; set; }
        public int AssignedByUserId { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
