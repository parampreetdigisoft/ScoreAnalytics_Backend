using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StaffOnly")]
    public class PillarController : ControllerBase
    {
        private readonly IPillarService _pillarService;
        public PillarController(IPillarService pillarService)
        {
            _pillarService = pillarService;
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
        [Authorize]
        [Route("Pillars")]
        public async Task<IActionResult> GetAll() => Ok(await _pillarService.GetAllAsync());

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var pillar = await _pillarService.GetByIdAsync(id);
            if (pillar == null) return NotFound();
            return Ok(pillar);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add([FromBody] Pillar pillar)
        {
            var result = await _pillarService.AddAsync(pillar);
            return Created($"/api/pillar/{result.PillarID}", result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePillarDto pillar)
        {
            var result = await _pillarService.UpdateAsync(id, pillar);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _pillarService.DeleteAsync(id);
            if (!success) return NotFound();
            return Ok();
        }

        [HttpPost("GetPillarsHistoryByUserId")]
        public async Task<IActionResult> GetPillarsHistoryByUserId([FromBody] GetCityPillarHistoryRequestDto requestDto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != requestDto.UserID)
                return Unauthorized("User ID not found.");

            var response = await _pillarService.GetPillarsHistoryByUserId(requestDto);
            return Ok(response);
        }

        [HttpGet("ExportPillarsHistoryByUserId")]
        [Authorize]
        public async Task<IActionResult> ExportPillarsHistoryByUserId([FromQuery] GetCityPillarHistoryRequestDto requestDto)
        {
            var claimUserId = GetUserIdFromClaims();
            if (claimUserId == null || claimUserId != requestDto.UserID)
                return Unauthorized("User ID not found.");

            var content = await _pillarService.ExportPillarsHistoryByUserId(requestDto);

            return File(content.Item2 ?? new byte[1],
               "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
               content.Item1);
        }
        [HttpPost("GetResponsesByUserId")]
        public async Task<IActionResult> GetResponsesByUserId([FromBody] GetPillarResponseHistoryRequestNewDto requestDto)
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

            requestDto.UserId = claimUserId;
            var response = await _pillarService.GetResponsesByUserId(requestDto, userRole);
            return Ok(response);
        }
    }
} 