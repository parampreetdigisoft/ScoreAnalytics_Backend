using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StaffOnly")]
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
        [HttpPost("ImportAssessment")]
        [Authorize]
        public async Task<IActionResult> ImportAssessmentAsync(IFormFile file, [FromForm] int userID)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var content = await _responseService.ImportAssessmentAsync(file, userID);
            return Ok(content);
        }
        /// <summary>
        /// This API is used to get the city question history  gloabal history for admin
        /// </summary>
        /// <param name="cityID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getCityQuestionHistory")]
        [Authorize]
        public async Task<IActionResult> GetCityQuestionHistory([FromQuery] UserCityRequstDto userCityRequstDto)
        {
            var result = await _responseService.GetCityQuestionHistory(userCityRequstDto);
            return Ok(result);
        }
        [HttpGet]
        [Route("getAssessmentProgressHistory/{assessmentID}")]
        [Authorize]
        public async Task<IActionResult> getAssessmentProgressHistory(int assessmentID)
        {
            var result = await _responseService.GetAssessmentProgressHistory(assessmentID);
            return Ok(result);
        }

        [HttpPost]
        [Route("getCityPillarHistory")]
        [Authorize]
        public async Task<IActionResult> GetCityPillarHistory([FromBody] GetCityPillarHistoryRequestDto requestDto)
        {
            var result = await _responseService.GetCityPillarHistory(requestDto);
            return Ok(result);
        }

        [HttpPost]
        [Route("changeAssessmentStatus")]
        [Authorize]
        public async Task<IActionResult> ChangeAssessmentStatus([FromBody] ChangeAssessmentStatusRequestDto requestDto)
        {
            var result = await _responseService.ChangeAssessmentStatus(requestDto);
            return Ok(result);
        }

        [HttpPost]
        [Route("transferAssessment")]
        [Authorize]
        public async Task<IActionResult> TransferAssessment([FromBody] TransferAssessmentRequestDto requestDto)
        {
            var result = await _responseService.TransferAssessment(requestDto);
            return Ok(result);
        }
    }
}