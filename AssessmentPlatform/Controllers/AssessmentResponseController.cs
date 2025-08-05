using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using AssessmentPlatform.IServices;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AssessmentResponseController : ControllerBase
    {
        private readonly IAssessmentResponseService _responseService;
        public AssessmentResponseController(IAssessmentResponseService responseService)
        {
            _responseService = responseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _responseService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var resp = await _responseService.GetByIdAsync(id);
            if (resp == null) return NotFound();
            return Ok(resp);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AssessmentResponse response)
        {
            var result = await _responseService.AddAsync(response);
            return Created($"/api/assessmentresponse/{result.ResponseID}", result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] AssessmentResponse response)
        {
            var result = await _responseService.UpdateAsync(id, response);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _responseService.DeleteAsync(id);
            if (!success) return NotFound();
            return Ok();
        }
    }
} 