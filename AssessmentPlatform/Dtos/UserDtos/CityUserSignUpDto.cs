using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class CityUserSignUpDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
        public bool IsConfrimed { get; set; } = false;
        public bool Is2FAEnabled { get; set; } = false;
    }
}
