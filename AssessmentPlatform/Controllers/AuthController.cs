using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssessmentPlatform.Models;
using AssessmentPlatform.Data;
using System.Linq;
using System;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly UserService _userService;
        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            _userService = new UserService(context);
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