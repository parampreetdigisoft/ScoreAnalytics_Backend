using System;
using System.Linq;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.IServices;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using AssessmentPlatform.Dtos.UserDtos;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IUserService _userService;
        public AuthController( IConfiguration config, IUserService userService)
        {
            _config = config;
            _userService = userService;
        }
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterDto request)
        {
            if (request == null)
                return BadRequest("Invalid request data.");

            var existingUser = _userService.GetByEmail(request.Email);
            if (existingUser != null)
                return Conflict("User with this email already exists.");

            var newUser = _userService.Register(request.FullName, request.Email, request.PasswordHash, request.Role);

            if (newUser == null)
                return StatusCode(500, "User registration failed due to a server error.");

            return Ok("User registered successfully.");
        }


        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _userService.GetByEmail(request.Email);
            if (user == null || !_userService.VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized();

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "supersecretkey"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "TestIssuer",
                audience: _config["Jwt:Audience"] ?? "TestAudience",
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );
            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
} 