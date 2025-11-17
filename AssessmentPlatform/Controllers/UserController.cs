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
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        [HttpGet]
        [Route("GetUserByRoleWithAssignedCity")]
        public async Task<IActionResult> GetUserByRoleWithAssignedCity([FromQuery] GetUserByRoleRequestDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != request.UserID)
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetUserByRoleWithAssignedCity(request));
        }

        [HttpGet]
        [Route("GetEvaluatorByAnalyst")]
        public async Task<IActionResult> GetEvaluatorByAnalyst([FromQuery] GetAssignUserDto request)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != request.UserID)
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetEvaluatorByAnalyst(request));
        }

        [HttpPost]
        [Route("updateUser")]
        public async Task<IActionResult> UpdateUser([FromForm] UpdateUserDto dto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != dto.UserID)
                return Unauthorized("User ID not found.");

            return Ok(await _userService.UpdateUser(dto));
        }

        [HttpGet]
        [Route("getUserInfo")]
        public async Task<IActionResult> getUserInfo()
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null )
                return Unauthorized("User ID not found.");

            return Ok(await _userService.GetUserInfo(claimUserId.GetValueOrDefault()));
        }


        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("getUsersAssignedToCity/{cityID}")]
        public async Task<IActionResult> GetUsersAssignedToCity(int cityID) => Ok(await _userService.GetUsersAssignedToCity(cityID));
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