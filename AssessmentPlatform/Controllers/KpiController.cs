
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.Enums;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _kpiService;

        public KpiController(IKpiService kpiService)
        {
            _kpiService = kpiService;
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

        [HttpGet]
        [Route("GetAnalyticalLayerResults")]
        public async Task<IActionResult> GetAnalyticalLayerResults([FromQuery] GetAnalyticalLayerRequestDto response)
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

            var tierName = GetTierFromClaims();
            if (tierName == null && userRole == UserRole.CityUser)
                return Unauthorized("You Don't have access.");

            if (!Enum.TryParse<TieredAccessPlan>(tierName, true, out var userPlan))
            {
                return Unauthorized("You Don't have access.");
            }

            var result = await _kpiService.GetAnalyticalLayerResults(response, userId.GetValueOrDefault(), userRole, userPlan);
            if (result == null)
            {
                return Unauthorized("You Don't have access.");
            }

            return Ok(result);
        }
        [HttpGet]
        [Route("GetAllKpi")]
        public async Task<IActionResult> GetAllKpi()
        {
            var result = await _kpiService.GetAllKpi();
            return Ok(result);
        }

        [HttpPost]
        [Route("compareCities")]
        [Authorize(Policy = "StaffOnly")]
        public async Task<IActionResult> CompareCities([FromBody] CompareCityRequestDto r)
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
           var result = await _kpiService.CompareCities(r, userId.GetValueOrDefault(), userRole);
            return Ok(result);
        }
    }
}
