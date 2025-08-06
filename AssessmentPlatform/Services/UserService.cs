using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices; 
using AssessmentPlatform.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace AssessmentPlatform.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly AppSettings _appSettings;
        private readonly JwtSetting _jwtSetting;
        private readonly IEmailService _emailService;
        public UserService(ApplicationDbContext context, IOptions<AppSettings> appSettings, IEmailService emailService, IOptions<JwtSetting> jwtSetting)
        {
            _context = context;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _jwtSetting = jwtSetting.Value;
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
        public async Task<User?> GetByEmailAysync(string email)
        {
            try
            {
                return await _context.Users.Where(u => u.Email == email).AsQueryable().FirstOrDefaultAsync();

            }
            catch (Exception ex)
            {

            }
            return null;
        }
        public bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        public async Task<ResultResponseDto> ForgotPassword(string email)
        {
            var user =  GetByEmail(email);
            if (user == null)
            {
                return ResultResponseDto.Failure(new string[] { "User not exist." });
            }
            else
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");

                string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;
                var isMailSent = await _emailService.SendEmailAsync(email, "Password Recovery", "~/Views/EmailTemplates/ChangePassword.cshtml", new { ResetPasswordUrl= passwordResetLink });
                if (isMailSent)
                {
                    user.ResetToken = token;
                    user.ResetTokenDate = DateTime.Now;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
                return ResultResponseDto.Success(new string[] { "Please check your email for change password." });

            }
        }
        public async Task<ResultResponseDto> ChangePassword(string passwordToken, string password)
        {
            if (!string.IsNullOrEmpty(passwordToken))
            {
                var user = await _context.Users.Where(u => u.ResetToken == passwordToken).FirstOrDefaultAsync();
                
                if (user == null)
                {
                    return ResultResponseDto.Failure(new string[] { "User not exist." });
                }
                if (_appSettings.LinkValidHours >= (DateTime.Now - user.ResetTokenDate).Hours)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        var hash = BCrypt.Net.BCrypt.HashPassword(password);
                        user.PasswordHash = hash;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();

                        return ResultResponseDto.Success(new string[] { "Password updated successfully" });
                    }
                    else
                    {
                        return ResultResponseDto.Failure(new string[] { "Password cannot be null" });
                    }
                }
                else
                {
                    return ResultResponseDto.Failure(new string[] { "Link has been expired." });
                }
            }
            else
            {
                return ResultResponseDto.Failure(new string[] { "Link has been expired." });
            }
        }
        public async Task<UserResponseDto> Login(string email, string password)
        {
            var user = GetByEmail(email);
            await Task.Delay(100); // Simulate some delay for demonstration purposes
            if (user == null || !VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSetting.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var securityToken = new JwtSecurityToken(
                issuer: _jwtSetting.Issuer,
                audience: _jwtSetting.Audience,
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );
             var token = new JwtSecurityTokenHandler().WriteToken(securityToken);

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                CreatedBy = user.CreatedBy,
                Token = token
            };
            return response;
        }

        public async Task<ResultResponseDto> InviteUser(InviteUserDto inviteUser)
        {
            if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
            {
                return ResultResponseDto.Failure(new string[] { "Invalid request data." });
            }
            var existingUser = await GetByEmailAysync(inviteUser.Email);
            if (existingUser != null)
            {
                return ResultResponseDto.Failure(new string[] { "User already exists." });
            }
            var user = Register(inviteUser.FullName, inviteUser.Email, inviteUser.Password, inviteUser.Role);
            if (user == null)
            {
                return ResultResponseDto.Failure(new string[] { "Failed to register user." });
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
            var passwordToken = hash;
            var token = passwordToken.Replace("+", " ");

            string sub = $"Invitation to Assessment Platform as a {inviteUser.Role.ToString()}";
            string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;
            var isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", new { ResetPasswordUrl = passwordResetLink });
            if (isMailSent)
            {
                user.ResetToken = token;
                user.ResetTokenDate = DateTime.Now;
                user.CreatedBy = inviteUser.InvitedUserID;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
            return ResultResponseDto.Success(new string[] { "Invitation sent successfully." });
        }
    }
}