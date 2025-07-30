using System;

namespace AssessmentPlatform.Models
{
    public enum UserRole { Admin, Evaluator, Analyst, CityUser }
    public class User
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 