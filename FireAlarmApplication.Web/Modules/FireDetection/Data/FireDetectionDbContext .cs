using FireAlarmApplication.Shared.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.FireDetection.Data
{
    public class FireDetectionDbContext : DbContext
    {
        public FireDetectionDbContext(DbContextOptions<FireDetectionDbContext> options) : base(options) { }

        public DbSet<Models.FireDetection> FireDetections { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Models.FireDetection>(entity =>
            {
                entity.ToTable("fire_detections", "fire_detection");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Location)

                .HasColumnType("geometry(Point,4326)")
                .IsRequired()
                .HasComment("Geographic location using WGS84 coordinate system");


                // Time-based queries için
                entity.HasIndex(e => e.DetectedAt)
                      .HasDatabaseName("ix_fire_detections_detected_at");

                // Status queries için  
                entity.HasIndex(e => e.Status)
                      .HasDatabaseName("ix_fire_detections_status");

                // Composite index - active fires with recent detection
                entity.HasIndex(e => new { e.Status, e.DetectedAt })
                      .HasDatabaseName("ix_fire_detections_status_detected_at");

                // Risk-based filtering için
                entity.HasIndex(e => e.RiskScore)
                      .HasDatabaseName("ix_fire_detections_risk_score");

                // 🏷️ Column Configurations
                entity.Property(e => e.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.DetectedAt)
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                entity.Property(e => e.Confidence)
                      .HasPrecision(5, 2); // 999.99 format

                entity.Property(e => e.Brightness)
                      .HasPrecision(8, 2); // 999999.99 format

                entity.Property(e => e.FireRadiativePower)
                      .HasPrecision(10, 3); // 9999999.999 format

                entity.Property(e => e.RiskScore)
                      .HasPrecision(5, 2)
                      .HasDefaultValue(0);

                entity.Property(e => e.Satellite)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(e => e.Status)
                      .HasConversion<int>() // Enum -> int
                      .HasDefaultValue(FireStatus.Detected);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.UpdatedAt)
                      .ValueGeneratedOnAddOrUpdate();
            });
        }

        /// <summary>
        /// SaveChanges override - UpdatedAt otomatik update
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }
        /// <summary>
        /// SaveChangesAsync override - UpdatedAt otomatik update
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Modified entity'lerin UpdatedAt field'ını güncelle
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<Models.FireDetection>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
