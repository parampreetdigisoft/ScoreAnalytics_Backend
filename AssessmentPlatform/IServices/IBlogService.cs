using AssessmentPlatform.Dtos.blogDto;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CommonDto;

namespace AssessmentPlatform.IServices
{
    public interface IBlogService
    {
        Task<PaginationResponse<BlogResponseDto>> GetBlogs(PaginationRequest request);
        Task<ResultResponseDto<BlogResponseDto>> GetBlogByIdAsync(int id);
        Task<ResultResponseDto<bool>> AddUpdateBlog(AddUpdateBlogDto blog);
        Task<ResultResponseDto<bool>> DeleteBlog(int blogID);
        Task<PaginationResponse<BlogResponseDto>> GetPublicUsersBlogs(PaginationRequest request);
    }
    
}
