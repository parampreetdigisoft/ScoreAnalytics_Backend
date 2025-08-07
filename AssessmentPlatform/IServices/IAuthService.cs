using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IAuthService
    {
        User Register(string fullName, string email, string phn, string password, UserRole role);
        User GetByEmail(string email);
        Task<User?> GetByEmailAysync(string email);
        bool VerifyPassword(string password, string hash);
        Task<UserResponseDto> Login(string email, string password);
        Task<ResultResponseDto> ForgotPassword(string email);
        Task<ResultResponseDto> ChangePassword(string passwordToken, string password);
        Task<ResultResponseDto> InviteUser(InviteUserDto inviteUser);
    }
}
