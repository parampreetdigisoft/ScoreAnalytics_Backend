using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class UpdateUserDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        // public string Email { get; set; }
        public IFormFile? ProfileImage { get; set; }  
    }
    public class UpdateUserResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        // public string Email { get; set; }
        public string? ProfileImagePath { get; set; }
    }
}
