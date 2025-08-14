
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
        public IActionResult ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = _authService.ForgotPassword(request.Email);

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
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
} 