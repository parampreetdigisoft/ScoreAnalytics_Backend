using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityUserController : ControllerBase
    {
        public readonly ICityUserService _cityUserService;
        public CityUserController(ICityUserService cityUserService) 
        {
            _cityUserService = cityUserService;
        }


        [HttpPost("GetPillarsHistoryByUserId")]
        public async Task<IActionResult> GetPillarsHistoryByUserId([FromBody] GetCityPillarHistoryRequestDto requestDto)
        {
            //var response = await _cityUserService.GetPillarsHistoryByUserId(requestDto);
            return Ok();
        }
    }
}
