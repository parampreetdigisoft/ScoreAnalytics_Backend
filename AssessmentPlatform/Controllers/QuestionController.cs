
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "StaffOnly")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _questionService;
        public QuestionController(IQuestionService questionService)
        {
            _questionService = questionService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet("pillars")]
        [Authorize]
        public async Task<IActionResult> GetPillars() => Ok(await _questionService.GetPillarsAsync());

        [HttpGet("getQuestions")]
        [Authorize]
        public async Task<IActionResult> GetQuestions([FromQuery] GetQuestionRequestDto requestDto) => Ok(await _questionService.GetQuestionsAsync(requestDto));

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddQuestion([FromBody] Question q)
        {
            var result = await _questionService.AddQuestionAsync(q);
            return Ok(result);
        }
        [HttpPost("addUpdateQuestion")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddUpdateQuestion([FromBody] AddUpdateQuestionDto q)
        {
            var result = await _questionService.AddUpdateQuestion(q);
            return Ok(result);
        }

        [HttpPost("addBulkQuestions")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddBulkQuestions([FromBody] AddBulkQuestionsDto q)
        {
            var result = await _questionService.AddBulkQuestion(q);
            return Ok(result);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditQuestion(int id, [FromBody] Question q)
        {
            var result = await _questionService.EditQuestionAsync(id, q);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var success = await _questionService.DeleteQuestionAsync(id);
            if (!success) return BadRequest("You don't have Access");
            return Ok();
        }

        [HttpGet("getQuestionsByCityMappingId")]
        [Authorize]
        public async Task<IActionResult> GetQuestionsByCityIdAsync([FromQuery] CityPillerRequestDto requestDto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
                return Unauthorized("User ID not found in token.");

            var result = await _questionService.GetQuestionsByCityIdAsync(requestDto, userId.GetValueOrDefault());
            if (result == null) return NotFound();

            return Ok(result);
        }
        
        [HttpGet("ExportAssessment/{userCityMappingID}")]
        [Authorize]
        public async Task<IActionResult> ExportAssessment(int userCityMappingID)
        {
            var content = await _questionService.ExportAssessment(userCityMappingID);

            return File(content.Item2 ?? new byte[1],
               "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
               content.Item1);
        }

        [HttpGet("getQuestionsHistoryByPillar")]
        [Authorize]
        public async Task<IActionResult> GetQuestionsHistoryByPillar([FromQuery] GetCityPillarHistoryRequestDto requestDto)
        {
            var content = await _questionService.GetQuestionsHistoryByPillar(requestDto);

            return Ok(content);
        }
    }
}
