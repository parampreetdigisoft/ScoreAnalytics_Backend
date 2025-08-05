using AssessmentPlatform.Models;
using AssessmentPlatform.Data;
using System.Linq;
using BCrypt.Net;
using AssessmentPlatform.IServices; // Ensure you have installed the BCrypt.Net-Next NuGet package

namespace AssessmentPlatform.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public User Register(string fullName, string email, string password, UserRole role)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = hash,
                Role = role
            };
            _context.Users.Add(user);
            _context.SaveChanges();
            return user;
        }

        public User GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }

        public bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}