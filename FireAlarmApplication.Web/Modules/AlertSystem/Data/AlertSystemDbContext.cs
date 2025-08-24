using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Data
{
    /// <summary>
    /// AlertSystem module database context
    /// FireAlert, UserAlert, AlertFeedback, AlertRule entity'lerini yönetir
    /// </summary>
    public class AlertSystemDbContext : DbContext
    {
        public AlertSystemDbContext(DbContextOptions<AlertSystemDbContext> options) : base(options) { }

        // DbSets
        public DbSet<FireAlert> FireAlerts { get; set; } = null!;
        public DbSet<UserAlert> UserAlerts { get; set; } = null!;
        public DbSet<AlertFeedback> AlertFeedbacks { get; set; } = null!;
        public DbSet<AlertRule> AlertRules { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Schema
            modelBuilder.HasDefaultSchema("alert_system");

            // FireAlert Configuration
            modelBuilder.Entity<FireAlert>(entity =>
            {
                entity.ToTable("fire_alerts");
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.FireDetectionId)
                      .HasDatabaseName("ix_fire_alerts_fire_detection_id");

                entity.HasIndex(e => e.Status)
                      .HasDatabaseName("ix_fire_alerts_status");

                entity.HasIndex(e => new { e.Status, e.ExpiresAt })
                      .HasDatabaseName("ix_fire_alerts_status_expires");

                entity.HasIndex(e => new { e.CenterLatitude, e.CenterLongitude })
                      .HasDatabaseName("ix_fire_alerts_location");

                // Properties
                entity.Property(e => e.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.Title)
                      .HasMaxLength(200)
                      .IsRequired();

                entity.Property(e => e.Message)
                      .HasMaxLength(1000)
                      .IsRequired();

                entity.Property(e => e.LocationDescription)
                      .HasMaxLength(500);

                entity.Property(e => e.FeedbackSummary)
                      .HasMaxLength(500);

                entity.Property(e => e.Severity)
                      .HasConversion<int>();

                entity.Property(e => e.Status)
                      .HasConversion<int>()
                      .HasDefaultValue(AlertStatus.Active);

                entity.Property(e => e.OriginalConfidence)
                      .HasPrecision(5, 2);

                entity.Property(e => e.MaxRadiusKm)
                      .HasPrecision(10, 2);

                entity.Property(e => e.CenterLatitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.CenterLongitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.LastFeedbackAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.ResolvedAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.ExpiresAt)
                      .HasColumnType("timestamp with time zone")
                      .IsRequired();

                // Relationships
                entity.HasMany(e => e.UserAlerts)
                      .WithOne(ua => ua.FireAlert)
                      .HasForeignKey(ua => ua.FireAlertId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Feedbacks)
                      .WithOne(f => f.FireAlert)
                      .HasForeignKey(f => f.FireAlertId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // UserAlert Configuration
            modelBuilder.Entity<UserAlert>(entity =>
            {
                entity.ToTable("user_alerts");
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.UserId)
                      .HasDatabaseName("ix_user_alerts_user_id");

                entity.HasIndex(e => e.FireAlertId)
                      .HasDatabaseName("ix_user_alerts_fire_alert_id");

                entity.HasIndex(e => new { e.UserId, e.FireAlertId })
                      .HasDatabaseName("ix_user_alerts_user_fire")
                      .IsUnique(); // Bir kullanıcıya aynı alert için tek kayıt

                entity.HasIndex(e => new { e.IsDelivered, e.CreatedAt })
                      .HasDatabaseName("ix_user_alerts_delivery_status");

                // Properties
                entity.Property(e => e.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.UserRole)
                      .HasConversion<int>();

                entity.Property(e => e.UserLatitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.UserLongitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.DistanceToFireKm)
                      .HasPrecision(10, 2);

                entity.Property(e => e.AlertMessage)
                      .HasMaxLength(1000)
                      .IsRequired();

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.DeliveredAt)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.ReadAt)
                      .HasColumnType("timestamp with time zone");

                // Relationship
                entity.HasOne(e => e.FireAlert)
                      .WithMany(fa => fa.UserAlerts)
                      .HasForeignKey(e => e.FireAlertId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AlertFeedback Configuration
            modelBuilder.Entity<AlertFeedback>(entity =>
            {
                entity.ToTable("alert_feedbacks");
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.FireAlertId)
                      .HasDatabaseName("ix_alert_feedbacks_fire_alert_id");

                entity.HasIndex(e => e.UserId)
                      .HasDatabaseName("ix_alert_feedbacks_user_id");

                entity.HasIndex(e => new { e.FireAlertId, e.UserId })
                      .HasDatabaseName("ix_alert_feedbacks_alert_user")
                      .IsUnique(); // Bir kullanıcı bir alert için tek feedback

                entity.HasIndex(e => e.Type)
                      .HasDatabaseName("ix_alert_feedbacks_type");

                // Properties
                entity.Property(e => e.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.Type)
                      .HasConversion<int>();

                entity.Property(e => e.Comment)
                      .HasMaxLength(500);

                entity.Property(e => e.UserLatitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.UserLongitude)
                      .HasPrecision(10, 6);

                entity.Property(e => e.DistanceToFireKm)
                      .HasPrecision(10, 2);

                entity.Property(e => e.ReliabilityScore)
                      .HasDefaultValue(50);

                entity.Property(e => e.ConfidenceImpact)
                      .HasPrecision(5, 2)
                      .HasDefaultValue(0);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                // Relationship
                entity.HasOne(e => e.FireAlert)
                      .WithMany(fa => fa.Feedbacks)
                      .HasForeignKey(e => e.FireAlertId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AlertRule Configuration
            modelBuilder.Entity<AlertRule>(entity =>
            {
                entity.ToTable("alert_rules");
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.TargetUserRole)
                      .HasDatabaseName("ix_alert_rules_user_role");

                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName("ix_alert_rules_active");

                entity.HasIndex(e => new { e.TargetUserRole, e.IsActive })
                      .HasDatabaseName("ix_alert_rules_role_active");

                // Properties
                entity.Property(e => e.Name)
                      .HasMaxLength(100)
                      .IsRequired();

                entity.Property(e => e.Description)
                      .HasMaxLength(500);

                entity.Property(e => e.TargetUserRole)
                      .HasConversion<int>();

                entity.Property(e => e.MinConfidence)
                      .HasPrecision(5, 2);

                entity.Property(e => e.MaxDistanceKm)
                      .HasPrecision(10, 2);

                entity.Property(e => e.TitleTemplate)
                      .HasMaxLength(200)
                      .IsRequired();

                entity.Property(e => e.MessageTemplate)
                      .HasMaxLength(1000)
                      .IsRequired();

                entity.Property(e => e.IsActive)
                      .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp with time zone");
            });

            // Seed Data for AlertRules
            modelBuilder.Entity<AlertRule>().HasData(AlertRule.GetDefaultRules());
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
        /// Modified entity'lerin timestamp'lerini güncelle
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                // AlertRule UpdatedAt
                if (entry.Entity is AlertRule rule)
                {
                    rule.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}