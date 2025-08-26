using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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

        [HttpPost]
        [Route("saveAssessment")]
        public async Task<IActionResult> SaveAssessment([FromBody] AddAssessmentDto response)
        {
            var result = await _responseService.SaveAssessment(response);
            return Ok(result);
        }

        [HttpGet]
        [Route("getAssessmentResults")]
        [Authorize]
        public async Task<IActionResult> GetAssessmentResult([FromQuery] GetAssessmentRequestDto response)
        {
            var result = await _responseService.GetAssessmentResult(response);
            return Ok(result);
        }
        [HttpGet]
        [Route("getAssessmentQuestoins")]
        [Authorize]
        public async Task<IActionResult> GetAssessmentQuestoins([FromQuery] GetAssessmentQuestoinRequestDto response)
        {
            var result = await _responseService.GetAssessmentQuestion(response);
            return Ok(result);
        }
    }
}