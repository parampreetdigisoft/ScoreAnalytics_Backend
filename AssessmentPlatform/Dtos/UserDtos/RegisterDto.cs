using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; }
    }
}
