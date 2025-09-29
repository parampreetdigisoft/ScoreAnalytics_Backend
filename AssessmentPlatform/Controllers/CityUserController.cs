using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
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


        [HttpGet("getAllCities")]
        [AllowAnonymous]
        public async Task<IActionResult> getAllCities()
        {
            var response = await _cityUserService.getAllCities();
            return Ok(response);
        }
    }
}
