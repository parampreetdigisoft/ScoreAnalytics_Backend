
using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Dtos.UserDtos;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userService.Login(request.Email, request.Password);
            if (user == null)
                return Unauthorized();
            return Ok(user);
        }
        
        [HttpPost]
        [Route("ForgotPassword")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = _userService.ForgotPassword(request.Email);

            if (response == null)
                return StatusCode(500, "Password reset failed due to a server error.");

            return Ok(response);
        }

        [HttpPost]
        [Route("ChangePassword")]
        public IActionResult ChangePassword([FromBody] ChangedPasswordDto request)
        {
            if (request?.PasswordToken == null)
                return BadRequest("Invalid request data.");

            var response = _userService.ChangePassword(request.PasswordToken, request.Password);

            if (response == null)
                return StatusCode(500, "User registration failed due to a server error.");

            return Ok(response);
        }

        [HttpPost]
        [Route("InviteUser")]
        public IActionResult InviteUser([FromBody] InviteUserDto request)
        {
            if (request?.Email == null)
                return BadRequest("Invalid request data.");

            var response = _userService.InviteUser(request);

            if (response == null)
                return StatusCode(500, "User Invitation failed due to a server error.");

            return Ok(response);
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
} 