
using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Pillar> Pillars { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AssessmentResponse> AssessmentResponses { get; set; }
    }
}
