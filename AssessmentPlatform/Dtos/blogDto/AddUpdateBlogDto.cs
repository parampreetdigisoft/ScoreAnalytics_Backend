namespace AssessmentPlatform.Dtos.blogDto
{
    public class AddUpdateBlogDto
    {
        public int? BlogID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
        public DateTime? PublishDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
