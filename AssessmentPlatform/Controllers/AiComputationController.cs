using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class AiComputationController : ControllerBase
    {

        private readonly IAIComputationService _aIComputationService;
        public AiComputationController(IAIComputationService aIComputationService)
        {
            _aIComputationService = aIComputationService;
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
            

        [HttpGet("getAICities")]
        public async Task<IActionResult> getAICities([FromQuery] AiCitySummeryRequestDto request)
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

            return Ok(await _aIComputationService.GetAICities(request, userId.Value, userRole));
        }

        [HttpGet("getAICityPillars/{cityID}")]
        public async Task<IActionResult> GetAICityPillars(int cityID)
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

            return Ok(await _aIComputationService.GetAICityPillars(cityID, userId.Value, userRole));
        }
    }
}
