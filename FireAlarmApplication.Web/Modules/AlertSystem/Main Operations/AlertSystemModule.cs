using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations
{
    public class AlertSystemModule : IFireGuardModule
    {
        public string ModuleName => "AlertSystem";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Database Context
            services.AddDbContext<AlertSystemDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    npgsqlOptions.CommandTimeout(60);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "alert_system");
                });
            });

            // Services
            services.AddScoped<IAlertService, AlertService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IUserAlertService, UserAlertService>();
            services.AddScoped<IGeofencingService, GeofencingService>();
            services.AddScoped<IAlertRuleService, AlertRuleService>();

            // DbContextFactory for GeofencingService
            services.AddDbContextFactory<UserManagementDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                });
            });
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            var alertGroup = endpoints.MapGroup("/api/alerts")
                .WithTags("Alert System")
                .WithOpenApi();

            // Alert endpoints
            alertGroup.MapGet("/active", GetActiveAlertsAsync)
                .WithName("GetActiveAlerts")
                .WithSummary("Get all active fire alerts");

            alertGroup.MapGet("/{alertId}", GetAlertByIdAsync)
                .WithName("GetAlertById")
                .WithSummary("Get specific alert details");

            alertGroup.MapPut("/{alertId}/status", UpdateAlertStatusAsync)
                .WithName("UpdateAlertStatus")
                .WithSummary("Update alert status")
                .RequireAuthorization();

            // User Alert endpoints
            alertGroup.MapGet("/user/{userId}", GetUserAlertsAsync)
                .WithName("GetUserAlerts")
                .WithSummary("Get alerts for specific user")
                .RequireAuthorization();

            alertGroup.MapPost("/user/{alertId}/read", MarkAlertAsReadAsync)
                .WithName("MarkAlertAsRead")
                .WithSummary("Mark alert as read")
                .RequireAuthorization();

            // Feedback endpoints
            alertGroup.MapPost("/{alertId}/feedback", SubmitFeedbackAsync)
                .WithName("SubmitFeedback")
                .WithSummary("Submit feedback for alert")
                .RequireAuthorization();

            // Admin endpoints
            alertGroup.MapPost("/cleanup", CleanupExpiredAlertsAsync)
                .WithName("CleanupExpiredAlerts")
                .WithSummary("Cleanup expired alerts")
                .RequireAuthorization(policy => policy.RequireRole("Admin"));

            alertGroup.MapGet("/stats", GetAlertStatsAsync)
                .WithName("GetAlertStats")
                .WithSummary("Get alert statistics")
                .RequireAuthorization(policy => policy.RequireRole("Admin"));
        }

        public async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AlertSystemDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AlertSystemModule>>();

            try
            {
                if (!await context.AlertRules.AnyAsync())
                {
                    await SeedAlertRulesAsync(context, logger);
                }

                // Test FireAlert seed (development)
                var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                if (isDevelopment && !await context.FireAlerts.AnyAsync())
                {
                    await SeedTestAlertsAsync(context, logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding AlertSystem module");
            }
        }

        // API Endpoint Handlers
        private static async Task<IResult> GetActiveAlertsAsync(IAlertService alertService)
        {
            var alerts = await alertService.GetActiveAlertsAsync();
            return Results.Ok(alerts);
        }

        private static async Task<IResult> GetAlertByIdAsync(
            Guid alertId,
            IAlertService alertService)
        {
            var alert = await alertService.GetAlertByIdAsync(alertId);
            if (alert == null)
                return Results.NotFound($"Alert {alertId} not found");

            return Results.Ok(alert);
        }

        private static async Task<IResult> UpdateAlertStatusAsync(
            Guid alertId,
            UpdateAlertStatusRequest request,
            IAlertService alertService)
        {
            var result = await alertService.UpdateAlertStatusAsync(alertId, request.Status);
            if (!result)
                return Results.BadRequest("Failed to update alert status");

            return Results.Ok(new { success = true, message = "Alert status updated" });
        }

        private static async Task<IResult> GetUserAlertsAsync(
            Guid userId,
            IUserAlertService userAlertService,
            bool onlyUnread = false)
        {
            var alerts = await userAlertService.GetUserAlertsAsync(userId, onlyUnread);
            return Results.Ok(alerts);
        }

        private static async Task<IResult> MarkAlertAsReadAsync(
            Guid alertId,
            HttpContext httpContext,
            IUserAlertService userAlertService)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await userAlertService.MarkAsReadAsync(alertId, userId);
            if (!result)
                return Results.BadRequest("Failed to mark alert as read");

            return Results.Ok(new { success = true });
        }

        private static async Task<IResult> SubmitFeedbackAsync(
            Guid alertId,
            FeedbackRequest request,
            HttpContext httpContext,
            AlertSystemDbContext context)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var feedback = new AlertFeedback
            {
                Id = Guid.NewGuid(),
                FireAlertId = alertId,
                UserId = userId,
                Type = request.Type,
                Comment = request.Comment,
                UserLatitude = request.Latitude,
                UserLongitude = request.Longitude,
                DistanceToFireKm = request.DistanceToFire,
                CreatedAt = DateTime.UtcNow
            };

            context.AlertFeedbacks.Add(feedback);
            await context.SaveChangesAsync();

            return Results.Ok(new { success = true, message = "Feedback submitted" });
        }

        private static async Task<IResult> CleanupExpiredAlertsAsync(IAlertService alertService)
        {
            var count = await alertService.CleanupExpiredAlertsAsync();
            return Results.Ok(new { success = true, cleanedUp = count });
        }

        private static async Task<IResult> GetAlertStatsAsync(AlertSystemDbContext context)
        {
            var stats = new
            {
                totalAlerts = await context.FireAlerts.CountAsync(),
                activeAlerts = await context.FireAlerts
                    .CountAsync(a => a.Status == AlertStatus.Active),
                totalUserAlerts = await context.UserAlerts.CountAsync(),
                deliveredAlerts = await context.UserAlerts
                    .CountAsync(ua => ua.IsDelivered),
                feedbackCount = await context.AlertFeedbacks.CountAsync(),
                alertsByStatus = await context.FireAlerts
                    .GroupBy(a => a.Status)
                    .Select(g => new { status = g.Key.ToString(), count = g.Count() })
                    .ToListAsync()
            };

            return Results.Ok(stats);
        }

        // Helper Methods
        private static Guid GetUserIdFromContext(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        // Seed Methods
        private static async Task SeedAlertRulesAsync(AlertSystemDbContext context, ILogger logger)
        {
            var defaultRules = AlertRule.GetDefaultRules();

            context.AlertRules.AddRange(defaultRules);
            await context.SaveChangesAsync();

            logger.LogInformation("🌱 Alert rules seeded: {Count} rules created", defaultRules.Count);
        }

        private static async Task SeedTestAlertsAsync(AlertSystemDbContext context, ILogger logger)
        {
            var testAlerts = new[]
            {
                new FireAlert
                {
                    Id = Guid.NewGuid(),
                    FireDetectionId = Guid.NewGuid(),
                    Title = "Test Yangın - Ankara Çankaya",
                    Message = "Test amaçlı yangın uyarısı",
                    LocationDescription = "Ankara, Çankaya",
                    Severity = AlertSeverity.Medium,
                    Status = AlertStatus.Active,
                    CenterLatitude = 39.9334,
                    CenterLongitude = 32.8597,
                    MaxRadiusKm = 20,
                    OriginalConfidence = 75,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                },
                new FireAlert
                {
                    Id = Guid.NewGuid(),
                    FireDetectionId = Guid.NewGuid(),
                    Title = "Test Yangın - Antalya Kemer",
                    Message = "Test amaçlı yüksek riskli yangın",
                    LocationDescription = "Antalya, Kemer",
                    Severity = AlertSeverity.High,
                    Status = AlertStatus.Active,
                    CenterLatitude = 36.5983,
                    CenterLongitude = 30.5760,
                    MaxRadiusKm = 30,
                    OriginalConfidence = 85,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    ExpiresAt = DateTime.UtcNow.AddHours(23)
                }
            };

            context.FireAlerts.AddRange(testAlerts);
            await context.SaveChangesAsync();

            logger.LogInformation("🌱 Test alerts seeded: {Count} alerts created", testAlerts.Length);
        }
    }

    // Request DTOs
    public class UpdateAlertStatusRequest
    {
        public AlertStatus Status { get; set; }
    }

    public class FeedbackRequest
    {
        public FeedbackType Type { get; set; }
        public string? Comment { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceToFire { get; set; }
    }
}