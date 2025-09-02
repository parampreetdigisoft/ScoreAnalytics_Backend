using System;

namespace AssessmentPlatform.Models
{
    public enum UserRole { Admin = 1, Analyst = 2, Evaluator = 3, CityUser = 4 }
    public class User
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? ResetToken { get; set; }
        public DateTime ResetTokenDate { get; set; } = DateTime.UtcNow;
        public bool IsEmailConfirmed { get; set; } = false;
        public string? ProfileImagePath { get; set; }
    }
}