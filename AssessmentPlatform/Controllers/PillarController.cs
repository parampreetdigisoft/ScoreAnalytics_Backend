using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using AssessmentPlatform.IServices;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PillarController : ControllerBase
    {
        private readonly IPillarService _pillarService;
        public PillarController(IPillarService pillarService)
        {
            _pillarService = pillarService;
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
        public async Task<IActionResult> Update(int id, [FromBody] Pillar pillar)
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
    }
} 