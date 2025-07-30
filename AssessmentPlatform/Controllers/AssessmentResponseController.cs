using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.Models;
using AssessmentPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssessmentResponseController : ControllerBase
    {
        private readonly IAssessmentResponseService _responseService;
        public AssessmentResponseController(IAssessmentResponseService responseService)
        {
            _responseService = responseService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll() => Ok(await _responseService.GetAllAsync());

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var resp = await _responseService.GetByIdAsync(id);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Add([FromBody] AssessmentResponse response)
        {
            var result = await _responseService.AddAsync(response);
            return Created($"/api/assessmentresponse/{result.ResponseID}", result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] AssessmentResponse response)
        {
            var result = await _responseService.UpdateAsync(id, response);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _responseService.DeleteAsync(id);
            if (!success) return NotFound();
            return Ok();
        }
    }
} 