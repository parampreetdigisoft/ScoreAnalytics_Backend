using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IUserService
    {
        User Register(string fullName, string email, string password, UserRole role);
        User GetByEmail(string email);
        bool VerifyPassword(string password, string hash);
    }
} 