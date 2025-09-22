using FireAlarmApplication.Web.Modules.FireDetection.Data;
using FireAlarmApplication.Web.Modules.FireDetection.Services;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.FireDetection.Modules;

/// <summary>
/// Fire Detection Module - Yangın tespit ve risk analizi
/// NASA FIRMS API'den veri alır, PostgreSQL'e kaydeder, event fırlatır
/// </summary>
public class FireDetectionModule : IFireGuardModule
{
    public string ModuleName => "FireDetection";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FireDetectionDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("FireDetectionDB");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.CommandTimeout(60);
            });
        });

        services.AddScoped<IFireDetectionService, FireDetectionService>();
        services.AddScoped<INasaFirmsService, NasaFirmsService>();
        services.AddScoped<IFireDataSyncService, FireDataSyncService>();
        services.AddScoped<IBackGroundJobService, BackGroundJobService>();
        services.AddScoped<IOsmGeoDataService, OSMGeoDataService>();
        services.AddScoped<IRiskCalculationService, RiskCalculationService>();
        //services.AddScoped<Services.IRiskCalculationService, Services.RiskCalculationService>();

        services.AddHttpClient<INasaFirmsService, NasaFirmsService>(client =>
        {
            var baseUrl = configuration["FireGuard:NasaFirms:BaseUrl"]
                          ?? "https://firms.modaps.eosdis.nasa.gov/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "FireGuard-Turkey/1.0");
            client.Timeout = TimeSpan.FromMinutes(5);

        });
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
        //Fire Detection API Endpoints
        var fireGroup = endpoints.MapGroup("/api/fires").WithTags("Fire Detection").WithOpenApi();

        // GET/api/fires
        // Aktif yangınlar
        fireGroup.MapGet("/", GetActiveFiresAsync)
                .WithName("GetActiveFires")
                .WithSummary("Get active fires in Turkey")
                .Produces<List<Models.FireDto>>();

        // GET/api/fires/near/{lat}/{lng}
        // Yakındaki yangınlar
        fireGroup.MapGet("/near/{lat:double}/{lng:double}", GetNearbyFiresAsync)
                .WithName("GetNearbyFires")
                .WithSummary("Get fires near specified location")
                .Produces<List<Models.FireDto>>();

        // GET/api/fires/stats
        // Yangın istatistikleri
        fireGroup.MapGet("/stats", GetFireStatsAsync)
                .WithName("GetFireStats")
                .WithSummary("Get fire statistics")
                .Produces<Models.FireStatsDto>();

        fireGroup.MapGet("/sync", TriggerManualSyncAsync)
            .WithName("TriggerManualSync")
            .WithSummary("Trigger manual NASA FIRMS data sync")
            .Produces<object>(StatusCodes.Status200OK);
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

    //API Endpoint Handlers
    private static async Task<IResult> GetActiveFiresAsync(IFireDetectionService fireService)
    {
        var fires = await fireService.GetActiveFiresAsync();
        return Results.Ok(fires);
    }

    private static async Task<IResult> GetNearbyFiresAsync(
        double lat, double lng,
        IFireDetectionService fireService,
        double radiusKm = 50)
    {
        var fires = await fireService.GetFiresNearLocationAsync(lat, lng, radiusKm);
        return Results.Ok(fires);
    }

    private static async Task<IResult> GetFireStatsAsync(IFireDetectionService fireService)
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

        logger.LogInformation("Test fire data seeded: {FireId} near Ankara", testFire.Id);
    }

    private static async Task<IResult> TriggerManualSyncAsync(IFireDataSyncService fireDetectionService)
    {
        try
        {
            //var jobId = backGroundJobService.TriggerManualSyncAsync();

            //return Results.Ok(new
            //{
            //    Success = true,
            //    Message = "Manual sync triggered successfully",
            //    JobId = jobId,
            //    Timestamp = DateTime.UtcNow
            //});

            var result = await fireDetectionService.SyncFiresFromNasaAsync();

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error triggering sync: {ex.Message}");
        }
    }
}