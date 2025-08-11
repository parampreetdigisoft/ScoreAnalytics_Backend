using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
    }
    public class InviteUserDto : RegisterDto
    {
        public int InvitedUserID { get; set; }
        public int CityID { get; set; }

    }
    public class UpdateInviteUserDto : InviteUserDto
    {
        public int UserID { get; set; }
    }
}
