using FireAlarmApplication.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Data
{
    public class UserManagementDbContext : DbContext
    {
        public UserManagementDbContext(DbContextOptions<UserManagementDbContext> options)
           : base(options) { }

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("user_management");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.Email)
                      .IsUnique()
                      .HasDatabaseName("ix_users_email");

                entity.HasIndex(e => e.PhoneNumber)
                      .HasDatabaseName("ix_users_phone");

                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName("ix_users_active");

                entity.HasIndex(e => e.Role)
                      .HasDatabaseName("ix_users_role");

                // Spatial indexes for PostGIS
                entity.HasIndex(e => e.CurrentLocation)
                      .HasMethod("GIST")
                      .HasDatabaseName("ix_users_current_location");

                entity.HasIndex(e => e.HomeLocation)
                      .HasMethod("GIST")
                      .HasDatabaseName("ix_users_home_location");

                // Properties
                entity.Property(e => e.Id)
                      .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.Email)
                      .IsRequired();

                entity.Property(e => e.Role)
                      .HasConversion<int>();

                entity.Property(e => e.CurrentLocation)
                      .HasColumnType("geometry(Point,4326)");

                entity.Property(e => e.HomeLocation)
                      .HasColumnType("geometry(Point,4326)");

                entity.Property(e => e.LocationAccuracy)
                      .HasPrecision(10, 2);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp with time zone")
                      .HasDefaultValueSql("now()");

                entity.Property(e => e.LastLocationUpdate)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.LastLoginAt)
                      .HasColumnType("timestamp with time zone");
            });
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<User>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public class UserManagementDbContextFactory : IDesignTimeDbContextFactory<UserManagementDbContext>
    {
        public UserManagementDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UserManagementDbContext>();

            // config dosyasını oku
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");

            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.CommandTimeout(60);
            });

            return new UserManagementDbContext(optionsBuilder.Options);
        }
    }

}

