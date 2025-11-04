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
        public DbSet<PillarAssessment> PillarAssessments { get; set; } = default!;
        public DbSet<City> Cities { get; set; } = default!;
        public DbSet<UserCityMapping> UserCityMappings { get; set; } = default!;
        public DbSet<AppLogs> AppLogs { get; set; } = default!;
        public DbSet<PaymentRecord> PaymentRecords { get; set; } = default!;
        public DbSet<PublicUserCityMapping> PublicUserCityMappings { get; set; } = default!;
        public DbSet<AnalyticalLayer> AnalyticalLayers { get; set; } = default!;
        public DbSet<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = default!;
        public DbSet<AnalyticalLayerResult> AnalyticalLayerResults { get; set; } = default!;
        public DbSet<CityUserKpiMapping> CityUserKpiMappings { get; set; } = default!;
        public DbSet<CityUserPillarMapping> CityUserPillarMappings { get; set; } = default!;

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
            modelBuilder.Entity<PillarAssessment>().HasKey(uc => uc.PillarAssessmentID);

            modelBuilder.Entity<Assessment>()
                .HasMany(r => r.PillarAssessments)
                .WithOne(a=>a.Assessment)
                .HasForeignKey(r => r.AssessmentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PillarAssessment>()
            .HasMany(r => r.Responses)
            .WithOne(a => a.PillarAssessment)
            .HasForeignKey(r => r.PillarAssessmentID)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<City>().HasKey(uc => uc.CityID);
            modelBuilder.Entity<PaymentRecord>(entity =>
            {
                entity.HasKey(p => p.PaymentRecordID);
                entity.Property(e => e.Tier)
                      .HasConversion<byte>();

                entity.Property(e => e.PaymentStatus)
                      .HasConversion<byte>();
            });

            modelBuilder.Entity<UserCityMapping>().HasKey(uc => uc.UserCityMappingID);
            modelBuilder.Entity<PublicUserCityMapping>().HasKey(uc => uc.PublicUserCityMappingID);

            modelBuilder.Entity<AnalyticalLayer>(entity =>
            {
                entity.HasKey(al => al.LayerID);

                entity.HasMany(al=>al.AnalyticalLayerResults)
                .WithOne(x=>x.AnalyticalLayer)
                .HasForeignKey(x=>x.LayerID);

                entity.HasMany(al => al.FiveLevelInterpretations)
               .WithOne(x => x.AnalyticalLayer)
               .HasForeignKey(x => x.LayerID);
            });
            modelBuilder.Entity<AnalyticalLayerResult>(entity =>
            {
                entity.HasKey(al => al.LayerResultID);
            });
            modelBuilder.Entity<FiveLevelInterpretation>(entity =>
            {
                entity.HasKey(al => al.InterpretationID);
            });

            modelBuilder.Entity<CityUserKpiMapping>().HasKey(ur => ur.CityUserKpiMappingID);
            modelBuilder.Entity<CityUserPillarMapping>().HasKey(ur => ur.CityUserPillarMappingID);

            base.OnModelCreating(modelBuilder);
        }

    }
}
