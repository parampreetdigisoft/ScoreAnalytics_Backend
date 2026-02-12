using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.blogDto;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.IServices; 
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssessmentPlatform.Services
{
    public class BlogService : IBlogService
    {
        #region constructor

        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        public BlogService(ApplicationDbContext context, IAppLogger appLogger, IWebHostEnvironment env, IMemoryCache cache)
        {
            _context = context;
            _appLogger = appLogger;
            _env = env;
            _cache = cache;
        }

        #endregion

        #region  methods Implementations

        public async Task<ResultResponseDto<bool>> AddUpdateBlog(AddUpdateBlogDto blogDto)
        {
            try
            {
                if (blogDto.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "assets/blogs");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    // ?? Remove old image if exists
                    if (!string.IsNullOrEmpty(blogDto.ImageUrl))
                    {
                        string oldFilePath = Path.Combine(_env.WebRootPath, blogDto.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(blogDto.ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await blogDto.ImageFile.CopyToAsync(stream);
                    }

                    blogDto.ImageUrl = "/assets/blogs/" + fileName;
                }

                var existing = await _context.Blogs
                .FirstOrDefaultAsync(x => x.BlogID == blogDto.BlogID && !x.IsDeleted);

                if (existing == null)
                {
                    // ADD
                    var newBlog = new Blog
                    {
                        Title = blogDto.Title,
                        Category = blogDto.Category,
                        Author = blogDto.Author,
                        Description = blogDto.Description,
                        ImageUrl = blogDto.ImageUrl,
                        PublishDate = blogDto.PublishDate,
                        IsActive = blogDto.IsActive,
                        IsDeleted = false,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.Blogs.AddAsync(newBlog);
                }
                else
                {
                    // UPDATE
                    existing.Title = blogDto.Title;
                    existing.Category = blogDto.Category;
                    existing.Author = blogDto.Author;
                    existing.Description = blogDto.Description;
                    existing.ImageUrl = blogDto.ImageUrl;
                    existing.PublishDate = blogDto.PublishDate;
                    existing.IsActive = blogDto.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;

                    _context.Blogs.Update(existing);
                }

                await _context.SaveChangesAsync();

                return ResultResponseDto<bool>.Success(true, new[] { "Blog saved successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in AddUpdateBlog", ex);
                return ResultResponseDto<bool>.Failure(new[] { "There is an error, please try later" });
            }
            
        }

        public async Task<ResultResponseDto<bool>> DeleteBlog(int blogID)
        {
            try
            {
                var blog = await _context.Blogs
                    .FirstOrDefaultAsync(x => x.BlogID == blogID && !x.IsDeleted);

                if (blog == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Blog not found" });

                blog.IsDeleted = true;
                blog.UpdatedAt = DateTime.UtcNow;

                _context.Blogs.Update(blog);
                await _context.SaveChangesAsync();

                return ResultResponseDto<bool>.Success(true, new[] { "Blog deleted successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in DeleteBlog", ex);
                return ResultResponseDto<bool>.Failure(new[] { "There is an error, please try later" });
            }
        }

        public async Task<ResultResponseDto<BlogResponseDto>> GetBlogByIdAsync(int id)
        {
            try
            {
                var blog = await _context.Blogs
                    .Where(x => x.BlogID == id && !x.IsDeleted)
                    .Select(x => new BlogResponseDto
                    {
                        BlogID = x.BlogID,
                        Title = x.Title,
                        Category = x.Category,
                        Author = x.Author,
                        Description = x.Description,
                        ImageUrl = x.ImageUrl,
                        PublishDate = x.PublishDate,
                        IsActive = x.IsActive,
                        UpdatedAt = x.UpdatedAt
                    })
                    .FirstOrDefaultAsync(x=>x.BlogID == id);

                if (blog == null)
                    return ResultResponseDto<BlogResponseDto>.Failure(new[] { "Blog not found" });

               // blog.Description = GetShortDescription(x.Description, int.MaxValue)
                return ResultResponseDto<BlogResponseDto>.Success(blog, new[] { "Blog fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetBlogByIdAsync", ex);
                return ResultResponseDto<BlogResponseDto>.Failure(new[] { "There is an error, please try later" });
            }
        }

        public async Task<PaginationResponse<BlogResponseDto>> GetBlogs(PaginationRequest request)
        {
            try
            {

                var data = await _context.Blogs
                    .Where(x => !x.IsDeleted)
                    .Select(x => new BlogResponseDto
                    {
                        BlogID = x.BlogID,
                        Title = x.Title,
                        Category = x.Category,
                        Author = x.Author,
                        Description = x.Description,
                        ImageUrl = x.ImageUrl,
                        PublishDate = x.PublishDate,
                        IsActive = x.IsActive,
                        UpdatedAt = x.UpdatedAt
                    }).ApplyPaginationAsync(request);

                return data;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetBlogs", ex);
                return new PaginationResponse<BlogResponseDto>();
            }
        }

        public async Task<ResultResponseDto<List<BlogResponseDto>>> GetPublicUsersBlogs()
        {
            try
            {
                var cacheKey = $"public_blogs_{DateTime.UtcNow:yyyyMMdd}";

                var cachedResult = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                    var date = DateTime.UtcNow.Date;

                    var blogs = await _context.Blogs
                        .AsNoTracking() // VERY IMPORTANT for read-only
                        .Where(x => !x.IsDeleted && x.IsActive)
                        .Where(x => !x.PublishDate.HasValue || x.PublishDate.Value.Date == date)
                        .OrderByDescending(x => x.PublishDate)
                        .ToListAsync();

                    var result = blogs.Select((x, index) => new BlogResponseDto
                    {
                        BlogID = x.BlogID,
                        Title = x.Title,
                        Category = x.Category,
                        Author = x.Author,
                        Description = GetShortDescription(x.Description, index == 0 ? 500 : 300),
                        ImageUrl = x.ImageUrl,
                        PublishDate = x.PublishDate,
                        IsActive = x.IsActive,
                        UpdatedAt = x.UpdatedAt
                    }).ToList();

                    return result;
                });

                return ResultResponseDto<List<BlogResponseDto>>
                    .Success(cachedResult, new[] { "Blogs fetched successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPublicUsersBlogs", ex);

                return ResultResponseDto<List<BlogResponseDto>>
                    .Failure(new[] { "There is an error, please try later" });
            }
        }
        private string GetShortDescription(string html, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove HTML tags
            var withoutHtml = System.Text.RegularExpressions.Regex
                .Replace(html, "<.*?>", string.Empty);

            // Decode HTML entities
            withoutHtml = System.Net.WebUtility.HtmlDecode(withoutHtml);

            // Replace non-breaking space
            withoutHtml = withoutHtml.Replace("\u00a0", " ");

            if (withoutHtml.Length <= maxLength)
                return withoutHtml;

            return withoutHtml.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}
