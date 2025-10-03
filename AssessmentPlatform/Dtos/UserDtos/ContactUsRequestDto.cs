using System.ComponentModel.DataAnnotations;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class ContactUsRequestDto
    {
        public string Name { get; set; } 
        public string Email { get; set; } 
        public string City { get; set; } 
        public string Country { get; set; } 
        public string Subject { get; set; } 
        public string Message { get; set; } 
    }
}
