using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PaidCityUserOnly")]
    public class CityUserController : ControllerBase
    {
        public readonly ICityUserService _cityUserService;
        public CityUserController(ICityUserService cityUserService) 
        {
            _cityUserService = cityUserService;
        }

        [HttpGet]
        [Route("getCityHistory/{userID}/{updatedAt}")]
        public async Task<IActionResult> GetCityHistory()
        {
            // Get the UserId and Tier from the claims
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var tierClaim = User.FindFirst("Tier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim);

            var result = await _cityUserService.GetCityHistory(userId);
            return Ok(result);
        }
        [HttpGet]
        [Route("getCitiesProgressByUserId/{userID}/{updatedAt}")]
        public async Task<IActionResult> getCitiesProgressByUserId()
        {
            // Get the UserId and Tier from the claims
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var tierClaim = User.FindFirst("Tier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim);
            var result = await _cityUserService.GetCitiesProgressByUserId(userId);
            return Ok(result);
        }
        [HttpGet]
        [Route("getCityQuestionHistory")]
        public async Task<IActionResult> GetCityQuestionHistory([FromQuery] UserCityRequstDto userCityRequstDto)
        {
            // Get the UserId and Tier from the claims
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var tierClaim = User.FindFirst("Tier")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim);
            userCityRequstDto.UserID = userId;

            var result = await _cityUserService.GetCityQuestionHistory(userCityRequstDto);
            return Ok(result);
        }
    }
}
