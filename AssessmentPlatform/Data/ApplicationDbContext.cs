using Microsoft.EntityFrameworkCore;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<Pillar> Pillars { get; set; } = default!;
        public DbSet<Question> Questions { get; set; } = default!;
        public DbSet<AssessmentResponse> AssessmentResponses { get; set; } = default!;
        public DbSet<City> Cities { get; set; } = default!;
        public DbSet<UserCityMapping> UserCityMappings { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(ur => ur.UserID);

            modelBuilder.Entity<Pillar>().HasKey(uc => uc.PillarID);

            modelBuilder.Entity<Question>().HasKey(uc => uc.QuestionID);

            modelBuilder.Entity<AssessmentResponse>().HasKey(uc => uc.AssessmentID);

            modelBuilder.Entity<City>().HasKey(uc => uc.CityID);

            modelBuilder.Entity<UserCityMapping>().HasKey(uc => uc.Id);


            base.OnModelCreating(modelBuilder);
        }

    }
}
