
using AssessmentPlatform.Dtos.QuestionDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _questionService;
        public QuestionController(IQuestionService questionService)
        {
            _questionService = questionService;
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
            if (!success) return NotFound();
            return Ok();
        }
    }
}
