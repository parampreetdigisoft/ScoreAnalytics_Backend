using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "StaffOnly")]
    public class CityController : ControllerBase
    {
        private readonly ICityService _cityService;
        public CityController(ICityService cityService)
        {
            _cityService = cityService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        private string? GetTierFromClaims()
        {
            return User.FindFirst("Tier")?.Value;
        }
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet("cities")]
        public async Task<IActionResult> GetCities([FromQuery] PaginationRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            request.UserId = userId;
            return Ok(await _cityService.GetCitiesAsync(request, userRole));
        }

        [HttpGet("getAllCityByUserId/{userId}")]
        public async Task<IActionResult> getAllCityByUserId(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _cityService.getAllCityByUserId(claimUserId.GetValueOrDefault(), userRole));
        }

        [HttpGet("cities/{id}")]
        public async Task<IActionResult> GetByIdAsync(int id) => Ok(await _cityService.GetByIdAsync(id));

        [HttpPost("AddUpdateCity")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddUpdateCity([FromForm] AddUpdateCityDto q)
        {
            var result = await _cityService.AddUpdateCity(q);
            return Ok(result);
        }

        [HttpPost("addBulkCity")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBulkCity([FromBody] BulkAddCityDto q)
        {
            var result = await _cityService.AddBulkCityAsync(q);
            return Ok(result);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCity(int id, [FromBody] AddUpdateCityDto q)
        {
            var result = await _cityService.EditCityAsync(id, q);
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCity(int id)
        {
            var success = await _cityService.DeleteCityAsync(id);
            return Ok(success);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("assignCity")]
        public async Task<IActionResult> AssignCity([FromBody] UserCityMappingRequestDto q)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");

            q.UserId = userId.Value;
            var result = await _cityService.AssingCityToUser(q.UserId, q.CityId, q.AssignedByUserId);
            return Ok(result);
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("assignCity/{id}")]
        public async Task<IActionResult> EditAssignCity(int id, [FromBody] UserCityMappingRequestDto q)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != q.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _cityService.EditAssingCity(id, q.UserId,q.CityId,q.AssignedByUserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("unAssignCity")]
        public async Task<IActionResult> UnAssignCity([FromBody] UserCityUnMappingRequestDto requestDto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != requestDto.AssignedByUserId)
                return Unauthorized("User ID not found.");

            var result = await _cityService.UnAssignCity(requestDto);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCityByUserIdForAssessment/{userId}")]
        public async Task<IActionResult> GetCityByUserIdForAssessment(int userId)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != userId)
                return Unauthorized("User ID not found.");

            var result = await _cityService.GetCityByUserIdForAssessment(userId);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCityHistory/{userID}/{updatedAt}")]
        public async Task<IActionResult> GetCityHistory(int userID, DateTime updatedAt)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _cityService.GetCityHistory(claimUserId.GetValueOrDefault(), updatedAt, userRole);
            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("getCitiesProgressByUserId/{userID}/{updatedAt}")]
        public async Task<IActionResult> getCitiesProgressByUserId(int userID,DateTime updatedAt)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found.");

            var result = await _cityService.GetCitiesProgressByUserId(userID, updatedAt);
            return Ok(result);
        }

        [HttpGet("getAllCityByLocation")]
        public async Task<IActionResult> getAllCityByLocation([FromQuery] GetNearestCityRequestDto r) => Ok(await _cityService.getAllCityByLocation(r));

        [HttpGet("getAiAccessCity")]
        public async Task<IActionResult> GetAiAccessCity()
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null)
                return Unauthorized("User ID not found.");

            var role = GetRoleFromClaims();
            if (role == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(await _cityService.GetAiAccessCity(claimUserId.GetValueOrDefault(), userRole));
        }
    }
}
