using AssessmentPlatform.Dtos.CityDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityController : ControllerBase
    {
        private readonly ICityService _cityService;
        public CityController(ICityService cityService)
        {
            _cityService = cityService;
        }

        [HttpGet("cities")]
        [Authorize]
        public async Task<IActionResult> GetCities() => Ok(await _cityService.GetCitiesAsync());

        [HttpGet("cities/{id}")]
        [Authorize]
        public async Task<IActionResult> GetCities(int id) => Ok(await _cityService.GetByIdAsync(id));

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddQuestion([FromBody] City q)
        {
            var result = await _cityService.AddCityAsync(q);
            return Ok(result);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditQuestion(int id, [FromBody] City q)
        {
            var result = await _cityService.EditCityAsync(id, q);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var success = await _cityService.DeleteCityAsync(id);
            if (!success) return NotFound();
            return Ok();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("assignCity")]
        public async Task<IActionResult> AssignCity([FromBody] UserCityMappingRequestDto q)
        {
            var result = await _cityService.AssingCityToUser(q.UserId, q.CityId, q.AssignedByUserId);
            return Ok(result);
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("assignCity/{id}")]
        public async Task<IActionResult> EditAssignCity(int id, [FromBody] UserCityMappingRequestDto q)
        {
            var result = await _cityService.EditAssingCity(id, q.UserId,q.CityId,q.AssignedByUserId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete]
        [Authorize(Roles = "Admin")]
        [Route("assignCity/{id}")]
        public async Task<IActionResult> DeleteAssignCity(int id)
        {
            var result = await _cityService.DeleteAssingCity(id);

            return Ok(result);
        }
    }
}
