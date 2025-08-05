using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (_userService.GetByEmail(req.Email) != null)
                return BadRequest("User already exists");
            var user = _userService.Register(req.FullName, req.Email, req.Password, req.Role);
            return Created($"/api/user/{user.UserID}", new { user.UserID, user.FullName, user.Email, user.Role });
        }
    }

    public class RegisterRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
    }
} 