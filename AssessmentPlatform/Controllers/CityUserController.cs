using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.CityUserDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Enums;
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
        private readonly ICityUserService _cityUserService;

        public CityUserController(ICityUserService cityUserService)
        {
            _cityUserService = cityUserService;
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

        [HttpGet("getCityHistory")]
        public async Task<IActionResult> GetCityHistory()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");
            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var tier = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _cityUserService.GetCityHistory(userId.Value, tier);
            return Ok(result);
        }

        [HttpGet("getCitiesProgressByUserId")]
        public async Task<IActionResult> GetCitiesProgressByUserId()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var result = await _cityUserService.GetCitiesProgressByUserId(userId.Value);
            return Ok(result);
        }

        [HttpGet("getCityQuestionHistory")]
        public async Task<IActionResult> GetCityQuestionHistory([FromQuery] UserCityRequstDto userCityRequstDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCityRequstDto.UserID = userId.Value;
            userCityRequstDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _cityUserService.GetCityQuestionHistory(userCityRequstDto);
            return Ok(result);
        }

        [HttpGet("cities")]
        public async Task<IActionResult> GetCities([FromQuery] PaginationRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            request.UserId = userId.Value;

            var result = await _cityUserService.GetCitiesAsync(request);
            return Ok(result);
        }

        [HttpGet("getCityDetails")]
        public async Task<IActionResult> GetCityDetails([FromQuery] UserCityRequstDto userCityRequstDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCityRequstDto.UserID = userId.Value;
            userCityRequstDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _cityUserService.GetCityDetails(userCityRequstDto);
            return Ok(result);
        }


        [HttpGet("GetCityPillarDetails")]
        public async Task<IActionResult> GetCityPillarDetails([FromQuery] UserCityGetPillarInfoRequstDto userCityRequstDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            userCityRequstDto.UserID = userId.Value;
            userCityRequstDto.Tiered = Enum.Parse<TieredAccessPlan>(tierName);

            var result = await _cityUserService.GetCityPillarDetails(userCityRequstDto);
            return Ok(result);
        }
        [HttpGet("getCityUserCities")]
        public async Task<IActionResult> GetCityUserCities()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var response = await _cityUserService.GetCityUserCities(userId.Value);
            return Ok(response);
        }
        [HttpPost("addCityUserKpisCityAndPillar")]
        public async Task<IActionResult> AddCityUserKpisCityAndPillar([FromBody] AddCityUserKpisCityAndPillar b)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            if (!Enum.IsDefined(typeof(TieredAccessPlan), tierName))
                return Unauthorized("Invalid tier specified.");

            var response = await _cityUserService.AddCityUserKpisCityAndPillar(b, userId.GetValueOrDefault(), tierName);
            return Ok(response);
        }
        [HttpGet]
        [Route("getCityUserKpi")]
        public async Task<IActionResult> GetCityUserKpi()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _cityUserService.GetCityUserKpi(userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpPost]
        [Route("compareCities")]
        public async Task<IActionResult> CompareCities([FromBody] CompareCityRequestDto r)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            var result = await _cityUserService.CompareCities(r,userId.GetValueOrDefault(), tierName);
            return Ok(result);
        }

        [HttpGet("getAICityPillars")]
        public async Task<IActionResult> GetAICityPillars([FromQuery] AiCityPillarRequestDto request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var tierName = GetTierFromClaims();
            if (tierName == null)
                return Unauthorized("You Don't have access.");

            return Ok(await _cityUserService.GetAICityPillars(request, userId.Value, tierName));
        }
    }
}
