using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.FireDetection;

/// <summary>
/// Fire Detection Module - Yangın tespit ve risk analizi
/// NASA FIRMS API'den veri alır, PostgreSQL'e kaydeder, event fırlatır
/// </summary>
public class FireDetectionModule : IFireGuardModule
{
    public string ModuleName => "FireDetection";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 🗄️ PostgreSQL DbContext with PostGIS
        services.AddDbContext<Data.FireDetectionDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("FireDetectionDB");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // PostGIS extension for spatial data
                npgsqlOptions.UseNetTopologySuite();
                // Command timeout for large spatial queries
                npgsqlOptions.CommandTimeout(60);
            });

            // Development'ta detailed logging
            if (configuration.GetValue<bool>("Logging:LogLevel:FireGuard") is true)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<Services.IFireDetectionService, Services.FireDetectionService>();
        services.AddScoped<Services.INasaFirmsService, Services.NasaFirmsService>();
        services.AddScoped<Services.IRiskCalculationService, Services.RiskCalculationService>();

        services.AddHttpClient<Services.Interfaces.INasaFirmsService>(client =>
        {
            var baseUrl = configuration["FireGuard:NasaFirms:BaseUrl"] ?? "https://firms.modaps.eosdis.nasa.gov/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "FireGuard-Turkey/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
        // 🔌 Fire Detection API Endpoints
        var fireGroup = endpoints.MapGroup("/api/fires").WithTags("Fire Detection").WithOpenApi();

        // GET /api/fires - Aktif yangınlar
        fireGroup.MapGet("/", GetActiveFiresAsync)
                .WithName("GetActiveFires")
                .WithSummary("Get active fires in Turkey")
                .Produces<List<Models.FireDto>>();

        // GET /api/fires/near/{lat}/{lng} - Yakındaki yangınlar
        fireGroup.MapGet("/near/{lat:double}/{lng:double}", GetNearbyFiresAsync)
                .WithName("GetNearbyFires")
                .WithSummary("Get fires near specified location")
                .Produces<List<Models.FireDto>>();

        // GET /api/fires/stats - Yangın istatistikleri
        fireGroup.MapGet("/stats", GetFireStatsAsync)
                .WithName("GetFireStats")
                .WithSummary("Get fire statistics")
                .Produces<Models.FireStatsDto>();
    }

    public async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.FireDetectionDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FireDetectionModule>>();

        try
        {
            // Database oluştur (eğer yoksa)
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("🔥 FireDetection database ensured");

            // Test data ekle (development'ta)
            if (!await context.FireDetections.AnyAsync())
            {
                await SeedTestDataAsync(context, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error seeding FireDetection module");
        }
    }

    // 🔌 API Endpoint Handlers
    private static async Task<IResult> GetActiveFiresAsync(Services.Interfaces.IFireDetectionService fireService)
    {
        var fires = await fireService.GetActiveFiresAsync();
        return Results.Ok(fires);
    }

    private static async Task<IResult> GetNearbyFiresAsync(
        double lat, double lng,
        Services.Interfaces.IFireDetectionService fireService,
        double radiusKm = 50)
    {
        var fires = await fireService.GetFiresNearLocationAsync(lat, lng, radiusKm);
        return Results.Ok(fires);
    }

    private static async Task<IResult> GetFireStatsAsync(
        Services.Interfaces.IFireDetectionService fireService)
    {
        var stats = await fireService.GetFireStatsAsync();
        return Results.Ok(stats);
    }

    private static async Task SeedTestDataAsync(Data.FireDetectionDbContext context, ILogger logger)
    {
        // Ankara yakınında test yangını
        var testFire = new Models.FireDetection
        {
            Id = Guid.NewGuid(),
            Location = new NetTopologySuite.Geometries.Point(32.8597, 39.9334) { SRID = 4326 },
            DetectedAt = DateTime.UtcNow.AddHours(-2),
            Confidence = 85.5,
            Brightness = 325.7,
            FireRadiativePower = 12.3,
            Satellite = "MODIS",
            Status = FireAlarmApplication.Shared.Contracts.Enums.FireStatus.Active,
            RiskScore = 75.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.FireDetections.Add(testFire);
        await context.SaveChangesAsync();

        logger.LogInformation("🌱 Test fire data seeded: {FireId} near Ankara", testFire.Id);
    }
}