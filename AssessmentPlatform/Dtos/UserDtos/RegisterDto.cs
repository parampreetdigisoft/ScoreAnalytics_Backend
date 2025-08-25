using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; } = "sdfjru32brjfew";
        public UserRole Role { get; set; }
    }
    public class InviteUserDto : RegisterDto
    {
        public int InvitedUserID { get; set; }
        public List<int> CityID { get; set; } = new();

    }

    public class InviteBulkUserDto
    {
        public List<InviteUserDto> users { get; set; }
    }
    public class UpdateInviteUserDto : InviteUserDto
    {
        public int UserID { get; set; }
    }
}
