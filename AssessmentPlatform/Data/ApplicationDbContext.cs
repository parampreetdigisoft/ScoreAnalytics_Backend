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
        public DbSet<QuestionOption> QuestionOptions { get; set; } = default!;
        public DbSet<AssessmentResponse> AssessmentResponses { get; set; } = default!;
        public DbSet<Assessment> Assessments { get; set; } = default!;
        public DbSet<City> Cities { get; set; } = default!;
        public DbSet<UserCityMapping> UserCityMappings { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(ur => ur.UserID);

            modelBuilder.Entity<Pillar>().HasKey(uc => uc.PillarID);
            modelBuilder.Entity<Pillar>()
                .HasMany(q => q.Questions)
                .WithOne(qo => qo.Pillar)
                .HasForeignKey(qo => qo.PillarID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Question>().HasKey(uc => uc.QuestionID);
            modelBuilder.Entity<QuestionOption>().HasKey(qo => qo.OptionID);

            modelBuilder.Entity<Question>()
                .HasMany(q => q.QuestionOptions)
                .WithOne(qo => qo.Question)
                .HasForeignKey(qo => qo.QuestionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assessment>().HasKey(uc => uc.AssessmentID);
            modelBuilder.Entity<AssessmentResponse>().HasKey(uc => uc.ResponseID);

            modelBuilder.Entity<Assessment>()
                .HasMany(r => r.Responses)
                .WithOne(a=>a.Assessment)
                .HasForeignKey(r => r.AssessmentID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssessmentResponse>().HasKey(uc => uc.ResponseID);

            modelBuilder.Entity<City>().HasKey(uc => uc.CityID);

            modelBuilder.Entity<UserCityMapping>().HasKey(uc => uc.Id);


            base.OnModelCreating(modelBuilder);
        }

    }
}
