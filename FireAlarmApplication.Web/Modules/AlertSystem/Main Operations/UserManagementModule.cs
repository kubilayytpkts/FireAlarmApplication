using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations
{
    /// <summary>
    /// User Management Module - Kullanıcı yönetimi ve konum takibi
    /// Kullanıcı kayıt/login, konum güncelleme, bildirim tercihleri
    /// </summary>
    public class UserManagementModule : IFireGuardModule
    {
        public string ModuleName => "UserManagement";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<UserManagementDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite(); // Spatial data support
                    npgsqlOptions.CommandTimeout(60);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "user_management");
                });
            });

            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddAuthentication()
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                            System.Text.Encoding.ASCII.GetBytes(configuration["Jwt:Key"] ?? "default-key")),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            //User Management API Endpoints
            var userGroup = endpoints.MapGroup("/api/user")
                .WithTags("User Management")
                .WithOpenApi();

            // Authentication Endpoints
            userGroup.MapPost("/register", RegisterAsync)
                .WithName("Register")
                .WithSummary("Register new user")
                .AllowAnonymous();

            userGroup.MapPost("/login", LoginAsync)
                .WithName("Login")
                .WithSummary("User login")
                .AllowAnonymous();

            userGroup.MapPut("/profile", UpdateProfileAsync)
                .WithName("UpdateProfile")
                .WithSummary("Update user profile")
                .RequireAuthorization();

            // Location Endpoints
            userGroup.MapPost("/location", UpdateLocationAsync)
                .WithName("UpdateLocation")
                .WithSummary("Update user location")
                .RequireAuthorization();

            userGroup.MapPost("/location/batch", BatchLocationUpdateAsync)
                .WithName("BatchLocationUpdate")
                .WithSummary("Batch update locations")
                .RequireAuthorization();

            userGroup.MapGet("/location", GetLocationAsync)
                .WithName("GetLocation")
                .WithSummary("Get user location")
                .RequireAuthorization();

            userGroup.MapPost("/location/tracking", ToggleTrackingAsync)
                .WithName("ToggleTracking")
                .WithSummary("Toggle location tracking")
                .RequireAuthorization();

            // Admin Endpoints
            userGroup.MapGet("/nearby/{lat:double}/{lng:double}/{radius:double}", GetNearbyUsersAsync)
                .WithName("GetNearbyUsers")
                .WithSummary("Get users in radius (Admin)")
                .RequireAuthorization(policy => policy.RequireRole("Admin", "SystemAdmin"));

            userGroup.MapGet("/stats", GetUserStatsAsync)
                .WithName("GetUserStats")
                .WithSummary("Get user statistics")
                .RequireAuthorization(policy => policy.RequireRole("Admin", "SystemAdmin"));
        }

        public async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UserManagementDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserManagementModule>>();

            try
            {
                if (!await context.Users.AnyAsync())
                {
                    await SeedTestUsersAsync(context, logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding UserManagement module");
            }
        }

        private static async Task<IResult> RegisterAsync(RegisterRequest request, IUserManagementService userService)
        {
            var result = await userService.Register(request);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(result);
        }

        private static async Task<IResult> LoginAsync(LoginRequest request, IUserManagementService userService)
        {
            var result = await userService.Login(request);

            if (result.Success)
                return Results.Ok(result);

            return Results.Unauthorized();
        }

        private static async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request, IUserManagementService _userManagementService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await _userManagementService.UpdateProfile(userId, request);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(result);
        }

        private static async Task<IResult> UpdateLocationAsync(LocationUpdateRequest request, IUserManagementService userService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await userService.UpdateLocation(userId, request);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(result);
        }

        private static async Task<IResult> BatchLocationUpdateAsync(List<LocationUpdateRequest> requests, IUserManagementService userService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await userService.BatchLocationUpdate(userId, requests);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(result);
        }

        private static async Task<IResult> GetLocationAsync(IUserManagementService userService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await userService.GetLocation(userId);

            if (result.Success)
                return Results.Ok(result);

            return Results.NotFound(result);
        }

        private static async Task<IResult> ToggleTrackingAsync(TrackingRequest request, IUserManagementService userService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var result = await userService.ToggleTracking(userId, request);

            if (result.Success)
                return Results.Ok(result);

            return Results.BadRequest(result);
        }

        private static async Task<IResult> GetNearbyUsersAsync(double lat, double lng, double radius, IUserManagementService userService)
        {
            var users = await userService.FindUsersInRadiusAsync(lat, lng, radius);

            return Results.Ok(new
            {
                success = true,
                count = users.Count,
                users = users.Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Role,
                    latitude = u.Latitude,
                    longitude = u.Longitude,
                    lastUpdate = u.LastLocationUpdate
                })
            });
        }

        private static async Task<IResult> GetUserStatsAsync(UserManagementDbContext context)
        {
            var stats = new
            {
                totalUsers = await context.Users.CountAsync(),
                activeUsers = await context.Users.CountAsync(u => u.IsActive),
                trackingEnabled = await context.Users.CountAsync(u => u.IsLocationTrackingEnabled),
                byRole = await context.Users
                    .GroupBy(u => u.Role)
                    .Select(g => new { role = g.Key.ToString(), count = g.Count() })
                    .ToListAsync(),
                recentRegistrations = await context.Users
                    .CountAsync(u => u.CreatedAt > DateTime.UtcNow.AddDays(-7))
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

        public static async Task SeedTestUsersAsync(UserManagementDbContext context, ILogger logger)
        {
            var testUsers = new[]
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Email = "test.civilian@fireguard.com",
                    FirstName = "Test",
                    LastName = "Civilian",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    Role = UserRole.Civilian,
                    CurrentLocation = new NetTopologySuite.Geometries.Point(32.8597, 39.9334) { SRID = 4326 }, // Ankara
                    IsActive = true,
                    PhoneNumber ="05334546433",
                    IsLocationTrackingEnabled = true
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Email = "test.officer@fireguard.com",
                    FirstName = "Test",
                    LastName = "Officer",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    Role = UserRole.ForestOfficer,
                    CurrentLocation = new NetTopologySuite.Geometries.Point(30.7133, 36.8969) { SRID = 4326 }, // Antalya
                    IsActive = true,
                    PhoneNumber ="05334546434",
                    IsLocationTrackingEnabled = true
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Email = "test.fire@fireguard.com",
                    FirstName = "Test",
                    LastName = "FireDept",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    Role = UserRole.FireDepartment,
                    CurrentLocation = new NetTopologySuite.Geometries.Point(29.0297, 41.0082) { SRID = 4326 }, // İstanbul
                    IsActive = true,
                    PhoneNumber ="05334546435",
                    IsLocationTrackingEnabled = true
                }
            };

            context.Users.AddRange(testUsers);
            await context.SaveChangesAsync();

            logger.LogInformation("Test users seeded: {Count} users created", testUsers.Length);
        }
    }
}
