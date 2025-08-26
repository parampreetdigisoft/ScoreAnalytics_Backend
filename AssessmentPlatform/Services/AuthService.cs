using System.Data;
using System.Text;
using System.Security.Claims;
using AssessmentPlatform.Data;
using AssessmentPlatform.Models;
using Microsoft.Extensions.Options;
using AssessmentPlatform.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models.settings;

namespace AssessmentPlatform.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly AppSettings _appSettings;
        private readonly JwtSetting _jwtSetting;
        private readonly IEmailService _emailService;
        public AuthService(ApplicationDbContext context, IOptions<AppSettings> appSettings, IEmailService emailService, IOptions<JwtSetting> jwtSetting)
        {
            _context = context;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _jwtSetting = jwtSetting.Value;
        }

        public User Register(string fullName, string email, string phn, string password, UserRole role)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phn,
                PasswordHash = hash,
                Role = role,
                IsEmailConfirmed = false,
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
                return await _context.Users.Where(u => u.Email == email && !u.IsDeleted).AsQueryable().FirstOrDefaultAsync();

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
        public async Task<ResultResponseDto<object>> ForgotPassword(string email)
        {
            var user = GetByEmail(email);
            if (user == null)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
            }
            else
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");

                string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;
                var isMailSent = await _emailService.SendEmailAsync(email, "Password Recovery", "~/Views/EmailTemplates/ChangePassword.cshtml", new { ResetPasswordUrl = passwordResetLink });
                if (isMailSent)
                {
                    user.ResetToken = token;
                    user.ResetTokenDate = DateTime.Now;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
                return ResultResponseDto<object>.Success(new {},new string[] { "Please check your email for change password." });

            }
        }
        public async Task<ResultResponseDto<object>> ChangePassword(string passwordToken, string password)
        {
            var user = await _context.Users.Where(u => u.ResetToken == passwordToken).FirstOrDefaultAsync();

            if (user == null)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
            }
            if (_appSettings.LinkValidHours >= (DateTime.Now - user.ResetTokenDate).Hours)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(password);
                user.PasswordHash = hash;
                user.IsEmailConfirmed = true;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new {},new string[] { "Password updated successfully" });
            }
            else
            {
                return ResultResponseDto<object>.Failure(new string[] { "Link has been expired." });
            }
        }
        public async Task<ResultResponseDto<UserResponseDto>> Login(string email, string password)
        {
            var user = await GetByEmailAysync(email);
            if (user == null || !VerifyPassword(password, user.PasswordHash))
            {
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
            }
            var response = GetAuthorizedUserDetails(user);
            return response;
        }
        public ResultResponseDto<UserResponseDto> GetAuthorizedUserDetails(User user)
        {
            if (!user.IsEmailConfirmed || user.IsDeleted)
            {
                string message = $"Your mail is not confirmed or de-activated by super {(user.Role == UserRole.Analyst ? "Admin" : "Analyst")}";

                return ResultResponseDto<UserResponseDto>.Failure(new string[] { message });
            }
           var claims = new[]
           {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            var tokenExpired = DateTime.Now.AddHours(1);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSetting.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var securityToken = new JwtSecurityToken(
                issuer: _jwtSetting.Issuer,
                audience: _jwtSetting.Audience,
                claims: claims,
                expires: tokenExpired,
                signingCredentials: creds
            );
            var token = new JwtSecurityTokenHandler().WriteToken(securityToken);

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                IsDeleted = user.IsDeleted,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                CreatedBy = user.CreatedBy,
                IsEmailConfirmed = user.IsEmailConfirmed,
                TokenExpirationDate = tokenExpired,
                ProfileImagePath = user.ProfileImagePath,
                Token = token
            };
            return ResultResponseDto<UserResponseDto>.Success(response, new string[] { "Invitation sent successfully." });
        }

        public async Task<ResultResponseDto<object>> InviteUser(InviteUserDto inviteUser)
        {
            if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
            {
                return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
            }
            bool isExistingUser = true;
            var user =  GetByEmail(inviteUser.Email);

            if (user == null)
            {
                user = Register(inviteUser.FullName, inviteUser.Email, inviteUser.Phone, inviteUser.Password, inviteUser.Role);
                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Failed to register user." });
                }
                user.CreatedBy = inviteUser.InvitedUserID;
                isExistingUser = false;
            }
            if (user.Role != inviteUser.Role)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User already have different role" });
            }

            bool isMailSent = false;
            if (!user.IsEmailConfirmed)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");

                string sub = $"Invitation to Assessment Platform as a {inviteUser.Role.ToString()}";
                string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;
                isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", new { ResetPasswordUrl = passwordResetLink });
                user.ResetToken = token;
                user.ResetTokenDate = DateTime.Now;
                user.IsDeleted = false;
            }

            if (isMailSent || user.IsEmailConfirmed)
            {
                _context.Users.Update(user);

                foreach (var id in inviteUser.CityID)
                {
                    var mapping = new UserCityMapping
                    {
                        UserID = user.UserID,
                        CityID = id,
                        AssignedByUserId = inviteUser.InvitedUserID,
                        Role = user.Role
                    };
                    _context.UserCityMappings.Add(mapping);
                }
                await _context.SaveChangesAsync();

                string msg = string.Empty;

                if (isExistingUser && !user.IsEmailConfirmed)
                {
                    msg = "This user already exists. An invitation has been sent to confirm their email and access the assigned city.";
                }
                else if (!isExistingUser)
                {
                    msg = "User added successfully. An invitation has been sent to access the assigned city.";
                }
                else
                {
                    msg = "This user already exists and now have access to the assigned city.";
                }

                return ResultResponseDto<object>.Success(new {},new string[] { msg });
            }
            return ResultResponseDto<object>.Failure(new string[] { "User created but invitation not send due to server error" });
        }

        public async Task<ResultResponseDto<object>> UpdateInviteUser(UpdateInviteUserDto inviteUser)
        {
            if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
            {
                return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == inviteUser.UserID);

            if (user == null)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User not found." });
            }
            if(user.Role != inviteUser.Role)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User already have different role" });
            }
            bool isMailSent = true;

            string msg = "User updated successfully";
            if (user.Email != inviteUser.Email || (!user.IsEmailConfirmed || user.IsDeleted))
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");

                string sub = $"Invitation to Assessment Platform as a {inviteUser.Role.ToString()}";
                string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;
                isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", new { ResetPasswordUrl = passwordResetLink });

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Password);
                user.Email = inviteUser.Email;
                user.PasswordHash = passwordHash;
                user.IsEmailConfirmed = false;
                user.ResetToken = token;
                user.ResetTokenDate = DateTime.Now;
                user.IsDeleted = false;

                msg = "User updated and invitation sent successfully";
            }

            if (isMailSent)
            {
                user.FullName = inviteUser.FullName;
                user.Phone = inviteUser.Phone;
                user.CreatedBy = inviteUser.InvitedUserID;
                _context.Users.Update(user);

                var existingMappings = _context.UserCityMappings
                    .Where(m => m.UserID == user.UserID && m.AssignedByUserId == inviteUser.InvitedUserID && !m.IsDeleted)
                    .ToList();

                var existingCityIds = existingMappings.Select(m => m.CityID).ToList();

                var newCityIds = inviteUser.CityID;

                // Add missing cities
                var citiesToAdd = newCityIds.Except(existingCityIds).ToList();
                foreach (var cityId in citiesToAdd)
                {
                    var newMapping = new UserCityMapping
                    {
                        UserID = user.UserID,
                        CityID = cityId,
                        AssignedByUserId = inviteUser.InvitedUserID,
                        Role = user.Role
                    };
                    _context.UserCityMappings.Add(newMapping);
                }

                //Delete cities no longer in the new list
                var citiesToDelete = existingMappings
                    .Where(m => !newCityIds.Contains(m.CityID))
                    .ToList();
                foreach(var c in citiesToDelete)
                {
                    c.IsDeleted = true;
                    _context.UserCityMappings.Update(c);
                }

                // Save all changes
                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new {},new string[] { msg });
            }
            return ResultResponseDto<object>.Failure(new string[] { "User created but invitation not send due to server error" });
        }
        public async Task<ResultResponseDto<object>> DeleteUser(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(m => m.UserID == userId && !m.IsDeleted);
            if(user == null)
            {
                return ResultResponseDto<object>.Failure(new string[] { "User not exist" });
            }

            user.IsDeleted = true;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return ResultResponseDto<object>.Success(new { }, new string[] { "User deleted successfully" });
        }

        public async Task<ResultResponseDto<UserResponseDto>> RefreshToken(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x=>!x.IsDeleted && x.UserID == userId);
            if (user == null)
            {
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
            }
            var response = GetAuthorizedUserDetails(user);

            return await Task.FromResult(response);
        }

        public async Task<ResultResponseDto<object>> InviteBulkUser(InviteBulkUserDto inviteUserList)
        {
            try
            {
                if (inviteUserList?.users == null || !inviteUserList.users.Any())
                {
                    return ResultResponseDto<object>.Failure(new[] { "No users provided." });
                }

                // 1. Bulk fetch all users by email
                var emails = inviteUserList.users.Select(u => u.Email).ToList();
                var existingUsers = await _context.Users
                    .Where(u => emails.Contains(u.Email))
                    .ToDictionaryAsync(u => u.Email, u => u);

                // Collect new users & city mappings
                var newUsers = new List<User>();
                var newMappings = new List<UserCityMapping>();
                var emailTasks = new List<Task>();

                foreach (var inviteUser in inviteUserList.users)
                {
                    if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                    {
                        return ResultResponseDto<object>.Failure(new[] { "Invalid request data." });
                    }

                    // 2. Try get existing user
                    existingUsers.TryGetValue(inviteUser.Email, out var user);

                    // 3. Register if not exists
                    if (user == null)
                    {
                        user = new User
                        {
                            FullName = inviteUser.FullName,
                            Email = inviteUser.Email,
                            Phone = inviteUser.Phone,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Password),
                            Role = inviteUser.Role,
                            CreatedBy = inviteUser.InvitedUserID,
                            IsDeleted = false,
                        };
                        _context.Users.Add(user);
                        existingUsers[inviteUser.Email] = user; // add to dictionary for later mapping
                        await _context.SaveChangesAsync();
                    }

                    if (user.Role != inviteUser.Role)
                    {
                        return ResultResponseDto<object>.Failure(new[] { $"User {inviteUser.Email} already has a different role." });
                    }

                    // 5. Handle email invitation
                    if (!user.IsEmailConfirmed)
                    {
                        var token = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email).Replace("+", " ");
                        string resetLink = $"{_appSettings.ApplicationUrl}/auth/reset-password?PasswordToken={token}";

                        emailTasks.Add(_emailService.SendEmailAsync(
                            inviteUser.Email,
                            $"Invitation to Assessment Platform as a {inviteUser.Role}",
                            "~/Views/EmailTemplates/ChangePassword.cshtml",
                            new { ResetPasswordUrl = resetLink }
                        ));

                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                        user.IsDeleted = false;
                    }

                    // 6. Collect city mappings
                    var existingCityIds = _context.UserCityMappings
                        .Where(m => m.UserID == user.UserID && m.AssignedByUserId == inviteUser.InvitedUserID && !m.IsDeleted)
                        .Select(m => m.CityID)
                        .ToList();

                    var citiesToAdd = inviteUser.CityID.Except(existingCityIds).ToList();
                    foreach (var cityId in citiesToAdd)
                    {
                        newMappings.Add(new UserCityMapping
                        {
                            UserID = user.UserID,
                            CityID = cityId,
                            AssignedByUserId = inviteUser.InvitedUserID,
                            Role = user.Role
                        });
                    }
                }


                if (newMappings.Any()) await _context.UserCityMappings.AddRangeAsync(newMappings);
                await _context.SaveChangesAsync();

                // 8. Send all emails in parallel
                if (emailTasks.Any()) await Task.WhenAll(emailTasks);

                return ResultResponseDto<object>.Success(new { }, new[] { "Users will get invitation link to see assigned cities." });
            }
            catch (Exception ex) 
            {
                return ResultResponseDto<object>.Failure(new[] { ex.Message });
            }
        }
    }
}
