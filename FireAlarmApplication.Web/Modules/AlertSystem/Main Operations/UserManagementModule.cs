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
            var key = configuration["Jwt:Key"] ?? "fZ7@Qp1!vL4$rT9#xW2^mB8&nH6*kD3%Gy5+Jc0?SaEeUvYwRjFhZtPqLsMdNb";


            services.AddDbContextFactory<UserManagementDbContext>(options =>
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
                            System.Text.Encoding.ASCII.GetBytes(key)),
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

            userGroup.MapPut("/profile/password", UpdateUserPasswordAsync)
                .WithName("UpdateUserPassword")
                .WithSummary("Update user password")
                .RequireAuthorization();


            userGroup.MapGet("/profile", GetUserInformation)
               .WithName("GetUserInformation")
               .WithSummary("Get user information")
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
                    await SeedTestUsersAsync(context, logger);

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

        public static async Task<IResult> UpdateUserPasswordAsync(UpdateUserPasswordRequest updateUserPasswordRequest, IUserManagementService _userManagementService, HttpContext httpContext)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            return Results.Ok(await _userManagementService.UpdateUserPassword(userId, updateUserPasswordRequest));
        }

        public static async Task<IResult> GetUserInformation(HttpContext httpContext, IUserManagementService _userManagementService)
        {
            var userId = GetUserIdFromContext(httpContext);
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var userInformation = await _userManagementService.GetUserInformation(userId);
            if (userInformation == null)
                return Results.NotFound();

            return Results.Ok(userInformation);
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
            try
            {
                var roles = new[]
                 {
                UserRole.Civilian,
                UserRole.ForestOfficer,
                UserRole.FireDepartment
                 };

                var locations = new (double lon, double lat, string city)[]
                    {
                    (32.8597, 39.9334, "Ankara"),
                    (28.9784, 41.0082, "Istanbul"),
                    (27.1428, 38.4237, "Izmir"),
                    (30.7133, 36.8969, "Antalya"),
                    (28.3636, 36.8498, "Mugla"),
                    (35.3213, 37.0000, "Adana"),
                    (32.4846, 37.8746, "Konya"),
                    (29.0669, 37.7830, "Denizli"),
                    (29.4180, 40.1826, "Bursa"),
                    (30.5206, 39.7667, "Eskisehir"),
                    (39.9208, 32.8541, "Aksaray"),
                    (31.1667, 39.7500, "Afyonkarahisar"),
                    (38.4189, 27.1287, "Aydin"),
                    (43.0567, 39.7194, "Artvin"),
                    (30.6956, 36.8941, "Burdur"),
                    (30.2833, 40.7317, "Bilecik"),
                    (30.0665, 40.1467, "Bolu"),
                    (39.9208, 41.2769, "Erzurum"),
                    (39.4899, 39.7500, "Erzincan"),
                    (37.0662, 37.3833, "Gaziantep"),
                    (32.6472, 39.1400, "Kirikkale"),
                    (34.9556, 39.1458, "Kirşehir"),
                    (37.8667, 37.5833, "Kahramanmaras"),
                    (30.0500, 39.7667, "Kutahya"),
                    (39.9208, 39.0497, "Malatya"),
                    (34.6281, 39.7500, "Nevsehir"),
                    (38.6244, 39.4211, "Elazig"),
                    (40.6013, 39.9100, "Giresun"),
                    (39.7500, 30.5206, "Eskisehir"),
                    (41.0200, 39.7178, "Trabzon"),
                    (39.9208, 42.0242, "Kars"),
                    (38.4192, 43.0500, "Van"),
                    (41.0000, 39.8333, "Samsun"),
                    (36.8000, 34.6333, "Mersin"),
                    (33.6167, 37.0000, "Karaman"),
                    (36.7178, 37.0662, "Osmaniye"),
                    (37.0000, 37.8667, "Adiyaman"),
                    (38.7500, 30.5500, "Usak"),
                    (42.0267, 37.9144, "Bayburt"),
                    (41.2797, 41.0039, "Rize"),
                    (41.6686, 41.2722, "Sinop"),
                    (41.0167, 40.9833, "Sakarya"),
                    (41.4564, 41.6781, "Kastamonu"),
                    (40.1467, 39.9208, "Yozgat"),
                    (41.1833, 38.8667, "Tokat"),
                    (42.0000, 37.9144, "Gumushane"),
                    (42.1867, 38.6744, "Siirt"),
                    (43.0378, 38.4192, "Bitlis"),
                    (42.1897, 37.7644, "Batman"),
                    (43.1000, 37.4211, "Hakkari"),
                    (42.1800, 38.4950, "Mardin"),
                    (37.1583, 38.4950, "Sanliurfa"),
                    (40.2310, 36.7167, "Corum"),
                    (40.7500, 37.8667, "Amasya"),
                    (39.9208, 34.8044, "Kirikkale"),
                    (35.9083, 36.8000, "Hatay"),
                    (39.7667, 30.0665, "Bilecik"),
                    (39.9208, 41.2769, "Agri"),
                    (42.0267, 37.9144, "Ardahan"),
                    (44.0383, 37.7644, "Igdir"),
                    (39.7667, 41.0281, "Mus"),
                    (41.0200, 42.0000, "Artvin"),
                    (38.4950, 41.0167, "Bingol"),
                    (39.7678, 42.1897, "Bitlis"),
                    (38.4192, 44.0383, "Van"),
                    (41.2797, 39.7500, "Ordu"),
                    (39.9208, 43.0378, "Agri"),
                    (40.1826, 29.4180, "Bursa"),
                    (37.8667, 32.4846, "Konya"),
                    (38.4237, 27.1428, "Izmir"),
                    (40.6013, 35.3213, "Amasya"),
                    (38.6744, 36.8000, "Kahramanmaras"),
                    (36.8941, 36.8941, "Antalya"),
                    (37.0000, 37.1583, "Gaziantep"),
                    (41.0082, 29.0297, "Istanbul"),
                    (37.0662, 35.3213, "Adana"),
                    (39.9334, 32.8597, "Ankara"),
                    (38.4950, 27.1428, "Manisa"),
                    (41.0200, 40.6013, "Samsun"),
                    (39.9208, 30.5206, "Eskisehir"),
                    (38.6744, 34.6281, "Nevsehir"),
                    (37.8746, 32.4846, "Konya")
                    };

                var random = new Random();
                var testUsers = new List<User>();

                int totalUsers = 100;
                int citiesCount = locations.Length;

                for (int i = 0; i < citiesCount; i++)
                {
                    var role = roles[random.Next(roles.Length)];
                    var loc = locations[i];

                    var user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = $"test.{loc.city.ToLower()}.{role.ToString().ToLower()}.{Guid.NewGuid()}@fireguard.com",
                        FirstName = "Test",
                        LastName = loc.city,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                        Role = role,
                        CurrentLocation = new NetTopologySuite.Geometries.Point(loc.lon, loc.lat) { SRID = 4326 },
                        IsActive = true,
                        PhoneNumber = $"0533{random.Next(1000000, 9999999)}",
                        IsLocationTrackingEnabled = true
                    };

                    testUsers.Add(user);
                }

                for (int i = citiesCount; i < totalUsers; i++)
                {
                    var role = roles[random.Next(roles.Length)];
                    var loc = locations[random.Next(citiesCount)];

                    var user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = $"test.{loc.city.ToLower()}.{role.ToString().ToLower()}.{Guid.NewGuid().ToString().Substring(0, 8)}@fireguard.com",
                        FirstName = "Test",
                        LastName = loc.city,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                        Role = role,
                        CurrentLocation = new NetTopologySuite.Geometries.Point(loc.lon, loc.lat) { SRID = 4326 },
                        IsActive = true,
                        PhoneNumber = $"0533{random.Next(1000000, 9999999)}",
                        IsLocationTrackingEnabled = true
                    };

                    testUsers.Add(user);
                }

                context.Users.AddRange(testUsers);
                await context.SaveChangesAsync();

                logger.LogInformation("Test users seeded: {Count} users created", testUsers.Count);
            }
            catch (Exception ex)
            {

                throw ex;
            }

        }

    }
}
