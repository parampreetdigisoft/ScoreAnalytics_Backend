using AssessmentPlatform.Enums;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class UserResponseDto : PublicUserResponse
    {
        public DateTime TokenExpirationDate { get; set; } 
        public string? ProfileImagePath { get; set; }
        public string Token { get; set; }
        public TieredAccessPlan? tier { get; set; }
    }

    public class PublicUserResponse
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public bool IsDeleted { get; set; }
        public string Role { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsLoggedIn { get; set; }
    }
}
