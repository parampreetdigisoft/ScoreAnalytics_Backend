using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using AssessmentPlatform.Views.EmailModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AssessmentPlatform.Services
{
    public class AuthService : IAuthService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly AppSettings _appSettings;
        private readonly JwtSetting _jwtSetting;
        private readonly IEmailService _emailService;
        private readonly IAppLogger _appLogger;
        public AuthService(ApplicationDbContext context, IOptions<AppSettings> appSettings, IEmailService emailService, IOptions<JwtSetting> jwtSetting, IAppLogger appLogger)
        {
            _context = context;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _jwtSetting = jwtSetting.Value;
            _appLogger = appLogger;
        }
        #endregion

        #region IAuthService implemention

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
                Tier = role == UserRole.CityUser ? Enums.TieredAccessPlan.Pending : null
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
                await _appLogger.LogAsync("GetByEmailAysync", ex);
            }
            return null;
        }
        public bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        public async Task<ResultResponseDto<object>> ForgotPassword(string email)
        {
            try
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
                    var model = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        Title = "Password Recovery",
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = _appSettings.ApplicationUrl,
                        MsgText = "You are receiving this email because you recently requested a password reset for your USVI account."
                    };
                    var isMailSent = await _emailService.SendEmailAsync(email, "Password Recovery", "~/Views/EmailTemplates/ChangePassword.cshtml", model);
                    if (isMailSent)
                    {
                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                    }
                    return ResultResponseDto<object>.Success(new { }, new string[] { "Please check your email for change password." });

                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("ForgotPassword", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });

            }
        }
        public async Task<ResultResponseDto<object>> ChangePassword(string passwordToken, string password)
        {
            try
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

                    return ResultResponseDto<object>.Success(new { }, new string[] { "Password updated successfully" });
                }
                else
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Link has been expired." });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error change password", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<UserResponseDto>> Login(string email, string password)
        {
            try
            {
                var user = await GetByEmailAysync(email);
                if (user == null || !VerifyPassword(password, user.PasswordHash))
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
                }
                var response = GetAuthorizedUserDetails(user);
                return response;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error login", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { ex.Message });
            }

        }
        public ResultResponseDto<UserResponseDto> GetAuthorizedUserDetails(User user)
        {
            if (user == null)
            {
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request" });
            }
            if (!user.IsEmailConfirmed || user.IsDeleted)
            {
                string message = string.Empty;

                if (user.Role != UserRole.CityUser)
                {
                    message = $"Your mail is not confirmed or de-activated by super {(user.Role == UserRole.Analyst ? "Admin" : "Analyst")}";
                }
                else
                {
                    message = "Your email is not verified. Please check your inbox and click the verification link. If the link has expired, you can reset your password to verify your account.";
                }

                return ResultResponseDto<UserResponseDto>.Failure(new string[] { message });
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("Tier", user.Tier?.ToString() ?? ""),         
                new Claim("UserId", user!.UserID.ToString())       
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
                Token = token,
                tier = user.Tier
            };
            return ResultResponseDto<UserResponseDto>.Success(response, new string[] { "You have successfully logged in." });
        }

        public async Task<ResultResponseDto<object>> InviteUser(InviteUserDto inviteUser)
        {
            try
            {
                if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }
                bool isExistingUser = true;
                var user = GetByEmail(inviteUser.Email);

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

                var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                var passwordToken = hash;
                var token = passwordToken.Replace("+", " ");
                string sub = $"Invitation to Assessment Platform as a {inviteUser.Role.ToString()}";
                string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;

                var cityName = string.Join(", ",
                                         _context.Cities
                                         .Where(c => inviteUser.CityID.Contains(c.CityID))
                                         .Select(c => c.CityName));
                var invitedUser = _context.Users.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);

                var model = new EmailInvitationSendRequestDto
                {
                    ResetPasswordUrl = passwordResetLink,
                    ApiUrl = _appSettings.ApiUrl,
                    Title = sub,
                    ApplicationUrl = _appSettings.ApplicationUrl,
                    MsgText = $"You’ve been invited to join the VUI Assessment Platform as an {user.Role.ToString()}."+
                        $"This platform allows you to review indicators, contribute assessments, and collaborate with other experts in evaluating urban systems and sustainability data. ",
                    IsLoginBtn = false,
                    DescriptionAboutBtnText= "To get started, please click the button below to set your password and activate your account." +
                        "\r\n If you did not expect this invitation, you can safely ignore this email.",
                    BtnText = "Activate Account"
                };
                var isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", model);
                user.ResetToken = token;
                user.ResetTokenDate = DateTime.Now;
                user.IsDeleted = false;
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

                if (isMailSent)
                {
                    string msg = string.Empty;

                    if (isExistingUser)
                    {
                        msg = "This user already exists. An invitation has been sent to confirm their email and access the assigned city.";
                    }
                    else
                    {
                        msg = "User added successfully. An invitation has been sent to access the assigned city.";
                    }
                    return ResultResponseDto<object>.Success(new { }, new string[] { msg });
                }
                return ResultResponseDto<object>.Failure(new string[] { "User created but invitation not send due to server error" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<object>> UpdateInviteUser(UpdateInviteUserDto inviteUser)
        {
            try
            {
                if (inviteUser == null || string.IsNullOrEmpty(inviteUser.Email) || string.IsNullOrEmpty(inviteUser.FullName))
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Invalid request data." });
                }
                var userList = await _context.Users.Where(u => u.UserID == inviteUser.UserID || u.UserID == inviteUser.InvitedUserID).ToListAsync();

                var user = userList.FirstOrDefault(u => u.UserID == inviteUser.UserID);
                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not found." });
                }
                if (user.Role != inviteUser.Role)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User already have different role" });
                }


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
                foreach (var c in citiesToDelete)
                {
                    c.IsDeleted = true;
                    _context.UserCityMappings.Update(c);
                }

                // Save all changes
                await _context.SaveChangesAsync();

                bool isMailSent = false;
                var msgText = "You are receiving this email because you haven't reset your password";
                string msg = "User updated successfully";

                var invitedUser = userList.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);

                List<int> merged = inviteUser.CityID.Concat(citiesToDelete.Select(x => x.CityID)).ToList();

                var cities = await _context.Cities
                    .Where(c => merged.Contains(c.CityID))
                    .ToListAsync();

                if (citiesToAdd.Count > 0)
                {
                    isMailSent = true;
                    var invitedCityNames = string.Join(", ",
                        cities.Where(c => citiesToAdd.Contains(c.CityID)).Select(c => c.CityName));

                    msgText = $"You are receiving this email because {invitedUser?.FullName} recently requested city assignment ({invitedCityNames}) for your USVI account.";
                }

                if (citiesToDelete.Count > 0)
                {
                    var deleteName = cities
                    .Where(c => citiesToDelete.Select(x => x.CityID).Contains(c.CityID)).Select(c => c.CityName);
                    var deleteCityNames = string.Join(", ", deleteName);

                    if (isMailSent)
                    {
                        msgText += $" Additionally, you no longer have access to the cities ({deleteCityNames}) for your USVI account.";
                    }
                    else
                    {
                        msgText = $"You are receiving this email because {invitedUser?.FullName} recently removed your access to the following cities ({deleteCityNames}) for your USVI account.";
                    }
                    isMailSent = true;
                }
                if (isMailSent || !user.IsEmailConfirmed)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email);
                    var passwordToken = hash;
                    var token = passwordToken.Replace("+", " ");
                    string sub = $"Invitation to Assessment Platform as a {inviteUser.Role.ToString()}";
                    string passwordResetLink = _appSettings.ApplicationUrl + "/auth/reset-password?PasswordToken=" + token;

                    var model = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = _appSettings.ApplicationUrl,
                        MsgText = msgText,
                        Title = sub,
                        IsLoginBtn = false
                    };
                    isMailSent = await _emailService.SendEmailAsync(inviteUser.Email, sub, "~/Views/EmailTemplates/ChangePassword.cshtml", model);

                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(inviteUser.Password);
                    user.Email = inviteUser.Email;
                    user.PasswordHash = passwordHash;
                    user.IsEmailConfirmed = false;
                    user.ResetToken = token;
                    user.ResetTokenDate = DateTime.Now;
                    user.IsDeleted = false;

                    msg = "User updated and invitation sent successfully";
                    await _context.SaveChangesAsync();
                }

                return ResultResponseDto<object>.Success(new { }, new string[] { msg });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in UpdateInviteUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<object>> DeleteUser(int userId)
        {
            try
            {

                var user = await _context.Users.FirstOrDefaultAsync(m => m.UserID == userId && !m.IsDeleted);
                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist" });
                }
                user.IsDeleted = true;
                _context.Users.Update(user);

                var userMapping = _context.UserCityMappings.Where(x => x.UserID == userId).ToList();
                foreach (var m in userMapping)
                {
                    m.IsDeleted = true;
                    _context.UserCityMappings.Update(m);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<object>.Success(new { }, new string[] { "User deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in DeleteUser", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }

        public async Task<ResultResponseDto<UserResponseDto>> RefreshToken(int userId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserID == userId);
                if (user == null)
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new string[] { "Invalid request data." });
                }
                var response = GetAuthorizedUserDetails(user);

                return await Task.FromResult(response);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure RefreshToken", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new string[] { "There is an error please try later" });
            }
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

                    if (citiesToAdd.Count() > 0)
                    {
                        // 5. Handle email invitation
                        var token = BCrypt.Net.BCrypt.HashPassword(inviteUser.Email).Replace("+", " ");
                        string resetLink = $"{_appSettings.ApplicationUrl}/auth/reset-password?PasswordToken={token}";

                        var cityName = string.Join(", ",
                         _context.Cities
                         .Where(c => citiesToAdd.Contains(c.CityID))
                         .Select(c => c.CityName));
                        var invitedUser = _context.Users.FirstOrDefault(x => x.UserID == inviteUser.InvitedUserID);

                        var sub = $"Invitation to Assessment Platform as a {inviteUser.Role}";
                        var model = new EmailInvitationSendRequestDto
                        {
                            ResetPasswordUrl = resetLink,
                            ApiUrl = _appSettings.ApiUrl,
                            ApplicationUrl = _appSettings.ApplicationUrl,
                            Title = sub,
                            MsgText = $"You are receiving this email because {invitedUser?.FullName} recently requested city assignment ({cityName}) for your USVI account."
                        };

                        emailTasks.Add(_emailService.SendEmailAsync(
                            inviteUser.Email,
                            sub,
                            "~/Views/EmailTemplates/ChangePassword.cshtml",
                            model
                        ));

                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                        user.IsDeleted = false;
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
                await _appLogger.LogAsync("Invite Bulk User", ex);
                return ResultResponseDto<object>.Failure(new[] { ex.Message });
            }
        }

        public async Task<ResultResponseDto<string>> SendMailForEditAssessment(SendRequestMailToUpdateCity request)
        {
            try
            {
                var users = _context.Users.Where(x => x.UserID == request.MailToUserID || x.UserID == request.UserID);

                var mailToUser = users.FirstOrDefault(x => x.UserID == request.MailToUserID);
                if (mailToUser == null)
                {
                    return ResultResponseDto<string>.Failure(new string[] { "User not exist." });
                }
                else
                {
                    var user = users.FirstOrDefault(x => x.UserID == request.UserID);
                    var year = DateTime.Now.Year;
                    var assessment = await _context.Assessments.Include(x => x.UserCityMapping).FirstOrDefaultAsync(x => x.UserCityMappingID == request.UserCityMappingID && x.CreatedAt.Year== year);
                    if (assessment != null)
                    {
                        var city = _context.Cities.FirstOrDefault(x => x.CityID == assessment.UserCityMapping.CityID);

                        var url = string.Empty;
                        if (mailToUser.Role == UserRole.Admin)
                        {
                            url = $"admin/assesment/2/{assessment.UserCityMapping.CityID}";
                        }
                        else
                        {
                            url = $"analyst/evaluator-response/{request.UserID}/{assessment.UserCityMapping.CityID}";
                        }

                        string passwordResetLink = _appSettings.ApplicationUrl + url;
                        var model = new EmailInvitationSendRequestDto
                        {
                            ResetPasswordUrl = passwordResetLink,
                            Title = "Request to update city",
                            ApiUrl = _appSettings.ApiUrl,
                            ApplicationUrl = _appSettings.ApplicationUrl,
                            MsgText = $"You are receiving this email because user {user?.FullName} recently requested to update city {city?.CityName} from their USVI account.",
                            BtnText = "Give Access"
                        };
                        var isMailSent = await _emailService.SendEmailAsync(mailToUser.Email, "Request to update city", "~/Views/EmailTemplates/ChangePassword.cshtml", model);
                        if (isMailSent)
                        {
                            assessment.AssessmentPhase = AssessmentPhase.EditRequested;
                            _context.Assessments.Update(assessment);
                            await _context.SaveChangesAsync();
                            return ResultResponseDto<string>.Success("", new string[] { "You have requested to update the assessment" });
                        }
                    }
                    return ResultResponseDto<string>.Failure(new string[] { "There is an error please try again" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("ForgotPassword", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
        }
        #endregion
        public async Task<ResultResponseDto<UserResponseDto>> CityUserSignUp(CityUserSignUpDto request)
        {
            try
            {
                // Check if the user already exists
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return ResultResponseDto<UserResponseDto>.Failure(new[]
                    {
                        "An account with this email already exists. Please log in."
                    });
                }

                // Register new user
                var user = Register(request.FullName, request.Email, request.Phone, request.Password, request.Role);
                user.IsEmailConfirmed = request.IsConfrimed;

                bool isMailSend = false;
                // Send verification email
                if (!request.IsConfrimed)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(request.Email);
                    var token = hash.Replace("+", " "); // Replace + to avoid URL issues
                    var passwordResetLink = $"{_appSettings.ApplicationUrl}/auth/confirm-mail?PasswordToken={token}";

                    var emailModel = new EmailInvitationSendRequestDto
                    {
                        ResetPasswordUrl = passwordResetLink,
                        Title = "Verify Your Email",
                        ApiUrl = _appSettings.ApiUrl,
                        ApplicationUrl = _appSettings.ApplicationUrl,
                        MsgText = "Thank you for signing up! Please verify your email and reset your password to complete registration."
                    };

                    isMailSend = await _emailService.SendEmailAsync(
                        request.Email,
                        "Verify Your Email",
                        "~/Views/EmailTemplates/ChangePassword.cshtml",
                        emailModel
                    );
                    if (isMailSend)
                    {
                        user.ResetToken = token;
                        user.ResetTokenDate = DateTime.Now;
                    }
                }

                _context.Users.Update(user);

                var cum = new PublicUserCityMapping
                {
                    CityID = request.CityID,
                    UserID = user.UserID,
                    IsDeleted = false,
                    UpdatedAt = DateTime.Now
                };

                _context.PublicUserCityMappings.Add(cum);

                await _context.SaveChangesAsync();

                if (request.IsConfrimed)
                {
                    var response = GetAuthorizedUserDetails(user);
                    return response;
                }
                else if (isMailSend)
                {
                    return ResultResponseDto<UserResponseDto>.Success(new(), new[] 
                    { 
                        "We’ve sent you a verification link. Please check your email." 
                    });
                }
                else
                {
                    return ResultResponseDto<UserResponseDto>.Success(new(), new[] 
                    { 
                        "Email could not be sent. Please use 'Forgot Password' to generate a new one." 
                    });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error during user signup", ex);
                return ResultResponseDto<UserResponseDto>.Failure(new[] { "Something went wrong. Please try again later."});
            }
        }
        public async Task<ResultResponseDto<object>> ConfirmMail(string passwordToken)
        {
            try
            {
                var user = await _context.Users.Where(u => u.ResetToken == passwordToken).FirstOrDefaultAsync();

                if (user == null)
                {
                    return ResultResponseDto<object>.Failure(new string[] { "User not exist." });
                }
                if (_appSettings.LinkValidHours >= (DateTime.Now - user.ResetTokenDate).Hours)
                {
                    user.IsEmailConfirmed = true;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    return ResultResponseDto<object>.Success(new { }, new string[] { "Mail Confirmed Successfully, You Can Login Now!" });
                }
                else
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Link has been expired. You can reset your password" });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error change password", ex);
                return ResultResponseDto<object>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<object>> ContactUs(ContactUsRequestDto requestDto)
        {
            try
            {
                var emailModel = new EmailInvitationSendRequestDto
                {
                    ResetPasswordUrl = "",
                    Title = $"{requestDto.Subject} - {requestDto.Email}",
                    ApiUrl = _appSettings.ApiUrl,
                    ApplicationUrl = _appSettings.ApplicationUrl,
                    MsgText = requestDto.Message,
                    DescriptionAboutBtnText
                        = $"This email was sent by {requestDto.Name} from {requestDto.City}, {requestDto.Country}. You can reach them at: {requestDto.Email}.",
                    IsLoginBtn = false,
                    IsShowBtnText = false,
                };

                var isMailSend = await _emailService.SendEmailAsync(
                    _appSettings.ApplicationInfoMail,
                    requestDto.Subject,
                    "~/Views/EmailTemplates/ChangePassword.cshtml",
                    emailModel
                );

                if (isMailSend)
                {
                    return ResultResponseDto<object>.Success(
                        new { },
                        new string[] { "Thank you for contacting us. Our team will reach out to you shortly." }
                    );
                }
                else
                {
                    return ResultResponseDto<object>.Failure(new string[] { "Unable to send your message at the moment. Please try again later." });
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ContactUs", ex);
                return ResultResponseDto<object>.Failure(
                    new string[] { "An unexpected error occurred. Please try again later." }
                );
            }
        }

    }
}
