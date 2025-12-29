using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        [HttpGet("getAITrustLevels")]
        public async Task<IActionResult> GetAITrustLevels()
        {
            return Ok(await _aIComputationService.GetAITrustLevels());
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

        [HttpGet("getAIPillarQuestions")]
        public async Task<IActionResult> GetAIPillarQuestions([FromQuery] AiCityPillarSummeryRequestDto r)
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

            return Ok(await _aIComputationService.GetAIPillarsQuestion(r, userId.Value, userRole));
        }

        [HttpGet("{cityId}/aiCityDetailsReport")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadCityPdf(int cityId)
        {
            try
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

                IQueryable<AiCitySummeryDto> query = await _aIComputationService.GetCityAiSummeryDetails(userId ?? 0, userRole, cityId);

                var cityDetails = await query.FirstAsync();

                // Generate PDF
                var pdfBytes = await _aIComputationService.GenerateCityDetailsPdf(cityDetails);

                // Return PDF with proper headers
                var fileName = $"{cityDetails.CityName}_Details_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes,"application/pdf",fileName);
            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, new
                {
                    message = "Error generating PDF",
                    error = ex.Message
                });
            }
        }
    }
}
