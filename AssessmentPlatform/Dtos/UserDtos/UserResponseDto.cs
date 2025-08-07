using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class UserResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public string Token { get; set; }
    }
}
