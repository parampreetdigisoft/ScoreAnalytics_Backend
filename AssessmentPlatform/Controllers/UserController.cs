using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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

            return Created($"", new() { });
        }

        [HttpGet]
        [Route("GetUserByRoleWithAssignedCity")]
        public async Task<IActionResult> GetUserByRoleWithAssignedCity([FromQuery] GetUserByRoleRequestDto request) => Ok(await _userService.GetUserByRoleWithAssignedCity(request));

        [HttpGet]
        [Route("GetEvaluatorByAnalyst")]
        public async Task<IActionResult> GetEvaluatorByAnalyst([FromQuery] GetAssignUserDto request) => Ok(await _userService.GetEvaluatorByAnalyst(request));

        [HttpPost]
        [Route("updateUser")]
        public async Task<IActionResult> UpdateUser([FromForm] UpdateUserDto dto) => Ok(await _userService.UpdateUser(dto));
    }

    public class RegisterRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
    }
} 