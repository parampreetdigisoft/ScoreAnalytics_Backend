
using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _authService.Login(request.Email, request.Password);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }
        
        [HttpPost]
        [Route("forgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = await _authService.ForgotPassword(request.Email);

            if (response == null)
                return StatusCode(500, "Password reset failed due to a server error.");

            return Ok(response);
        }

        [HttpPost]
        [Route("changePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangedPasswordDto request)
        {
            if (request?.PasswordToken == null || request.Password == null)
                return BadRequest("Invalid request data.");

            var response = await _authService.ChangePassword(request.PasswordToken, request.Password);

            if (response == null)
                return StatusCode(500, "User registration failed due to a server error.");

            return Ok(response);
        }

        [HttpPost("CityUserSignUp")]
        public async Task<IActionResult> CityUserSignUp([FromBody] CityUserSignUpDto request)
        {
            var user = await _authService.CityUserSignUp(request);
            return Ok(user);
        }

        [HttpPost]
        [Route("InviteUser")]
        [Authorize]
        public async Task<IActionResult> InviteUser([FromBody] InviteUserDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = await _authService.InviteUser(request);

            if (response == null)
                return StatusCode(500, "User Invitation failed due to a server error.");

            return Ok(response);
        }
        [HttpPost]
        [Route("InviteBulkUser")]
        [Authorize]
        public async Task<IActionResult> InviteBulkUser([FromBody] InviteBulkUserDto request)
        {

            var response = await _authService.InviteBulkUser(request);

            if (response == null)
                return StatusCode(500, "User Invitation failed due to a server error.");

            return Ok(response);
        }

        [HttpPost]
        [Route("UpdateInviteUser")]
        [Authorize]
        public async Task<IActionResult> UpdateInviteUser([FromBody] UpdateInviteUserDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = await _authService.UpdateInviteUser(request);

            if (response == null)
                return StatusCode(500, "User Invitation failed due to a server error.");

            return Ok(response);
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (_authService.GetByEmail(req.Email) != null)
                return BadRequest("User already exists");
            var user = _authService.Register(req.FullName, req.Email, req.Phone, req.Password, req.Role);
            return Created($"/api/user/{user.UserID}", new { user.UserID, user.FullName, user.Email, user.Role });
        }

        [HttpDelete("deleteUser/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var result = await _authService.DeleteUser(userId);
            return Ok(result);
        }

        [HttpPost("refreshToken")]
        [Authorize]
        public async Task<IActionResult> RefreshToken([FromBody] UserIdDto request)
        {
            var user = await _authService.RefreshToken(request.UserID);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }

        [HttpPost("sendMailForEditAssessment")]
        [Authorize]
        public async Task<IActionResult> SendMailForEditAssessment([FromBody] SendRequestMailToUpdateCity request)
        {
            var user = await _authService.SendMailForEditAssessment(request);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }

        [HttpPost]
        [Route("confirmMail")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmMail([FromBody] ConfirmMailDto request)
        {
            if (request?.PasswordToken == null)
                return BadRequest("Invalid request data.");

            var response = await _authService.ConfirmMail(request.PasswordToken);

            if (response == null)
                return StatusCode(500, "Mail not confirmed due to a server error.");

            return Ok(response);
        }
        [HttpPost]
        [Route("contactus")]
        [AllowAnonymous]
        public async Task<IActionResult> ContactUs([FromBody] ContactUsRequestDto request)
        {
            var response = await _authService.ContactUs(request);

            if (response == null)
                return StatusCode(500, "Mail not confirmed due to a server error.");

            return Ok(response);
        }

        [HttpPost("twofaVerification")]
        public async Task<IActionResult> TwofaVerification([FromBody] TwofaVerificationRequest request)
        {
            var user = await _authService.TwofaVerification(request.Email, request.Otp);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }
        [HttpPost("reSendLoginOtp")]
        public async Task<IActionResult> ReSendLoginOtp([FromBody] EmailRequest request)
        {
            var user = await _authService.ReSendLoginOtp(request.Email);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }

        [HttpPost]
        [Route("updateUser")]
        [Authorize]
        public async Task<IActionResult> UpdateUser([FromForm] UpdateUserDto dto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != dto.UserID)
                return Unauthorized("You are not authorized.");

            return Ok(await _authService.UpdateUser(dto));
        }
    }

    public class LoginRequest : EmailRequest
    {
        public string Password { get; set; }
    }
    public class TwofaVerificationRequest : EmailRequest
    {
        public int Otp { get; set; }
    }
    public class EmailRequest
    {
        public string Email { get; set; }
    }
} 