
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Data;
using AssessmentPlatform.Models;
using System.Linq;
using System.Threading.Tasks;
using AssessmentPlatform.Services;

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

        [HttpGet("questions")]
        [Authorize]
        public async Task<IActionResult> GetQuestions() => Ok(await _questionService.GetQuestionsAsync());

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddQuestion([FromBody] Question q)
        {
            var result = await _questionService.AddQuestionAsync(q);
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
