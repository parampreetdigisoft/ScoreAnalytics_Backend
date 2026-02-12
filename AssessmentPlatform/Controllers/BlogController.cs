using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Dtos.blogDto;
using AssessmentPlatform.Dtos.CommonDto;
using Microsoft.AspNetCore.Authorization;

namespace AssessmentPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BlogController : ControllerBase
    {
        private readonly IBlogService _blogService;
        public BlogController(IBlogService blogService)
        {
            _blogService = blogService;
        }
        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return null;
        }
        private string? GetTierFromClaims()
        {
            return User.FindFirst("Tier")?.Value;
        }
        private string? GetRoleFromClaims()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("getBlogs")]
        public async Task<IActionResult> GetBlogs([FromQuery] PaginationRequest request)
        {
            var pillar = await _blogService.GetBlogs(request);
            if (pillar == null) return NotFound();
            return Ok(pillar);
        }
        [HttpGet]
        [Route("getBlogById/{blogID}")]
        public async Task<IActionResult> GetBlogByIdAsync(int blogID)
        {
            var pillar = await _blogService.GetBlogByIdAsync(blogID);
            if (pillar == null) return NotFound();
            return Ok(pillar);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("addUpdateBlog")]
        public async Task<IActionResult> AddUpdateBlog([FromForm] AddUpdateBlogDto request)
        {
            var result = await _blogService.AddUpdateBlog(request);
            return Ok(result);
        }

        [HttpDelete]
        [Authorize(Roles = "Admin")]
        [Route("deleteBlog/{blogID}")]
        public async Task<IActionResult> DeleteBlog(int blogID)
        {
            var result = await _blogService.DeleteBlog(blogID);
            return Ok(result);
        }

        [HttpGet]
        [Route("getPublicUsersBlogs")]
        public async Task<IActionResult> getPublicUsersBlogs() => Ok(await _blogService.GetPublicUsersBlogs());

    }
}
