using FireAlarmApplication.Web.Modules.FireDetection.Data;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class FireDataSyncService : IFireDataSyncService
    {
        private readonly IFireDetectionService _fireDecetionService;
        private readonly INasaFirmsService _nasaFirmsService;
        private readonly IMtgFireService _MtgFireService;
        private readonly FireDetectionDbContext _fireDetectionDbContext;
        private readonly IRedisService _redisService;
        private readonly ILogger<FireDataSyncService> _logger;
        private readonly NasaFirmsOptions _options;
        public FireDataSyncService(IFireDetectionService fireDecetionService, INasaFirmsService nasaFirmsService, FireDetectionDbContext fireDetectionDbContext
            , IRedisService redisService, ILogger<FireDataSyncService> logger, IOptions<NasaFirmsOptions> options, IMtgFireService mtgFireService)
        {
            _fireDecetionService = fireDecetionService;
            _nasaFirmsService = nasaFirmsService;
            _fireDetectionDbContext = fireDetectionDbContext;
            _redisService = redisService;
            _logger = logger;
            _options = options.Value;
            _MtgFireService = mtgFireService;
        }
        /// <summary>
        /// Global veri senkronizasyonu - MTG (Avrupa/Afrika) + NASA FIRMS (Amerika/Global)
        /// </summary>
        public async Task<int> SyncFiresFromNasaAsync()
        {
            try
            {
                _logger.LogInformation("Starting global fire data sync...");
                var allFires = new List<Models.FireDetection>();

                // 1. MTG - Avrupa, Afrika, Orta Doğu (hızlı, 10-20 dk latency)
                // Eumetsat MTG API bbox formatı: minLon,minLat,maxLon,maxLat
                var mtgRegions = new[]
                {
                    ("Europe/Turkey", "26,36,45,42"),      // Türkiye
                    ("Europe", "-15,35,30,72"),            // Batı Avrupa
                    ("Africa", "-20,-35,55,40"),           // Afrika
                    ("MiddleEast", "25,12,65,45")          // Orta Doğu
                };

                foreach (var (regionName, bbox) in mtgRegions)
                {
                    try
                    {
                        _logger.LogDebug("Fetching MTG data for {Region}...", regionName);
                        var fires = await _MtgFireService.FetchActiveFiresAsync(bbox, 86400);
                        if (fires.Any())
                        {
                            _logger.LogInformation("MTG {Region}: Found {Count} fires", regionName, fires.Count);
                            allFires.AddRange(fires);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "MTG fetch failed for {Region}, continuing...", regionName);
                    }
                }

                // 2. NASA FIRMS - Amerika ve diğer bölgeler (VIIRS/MODIS)
                // NASA FIRMS bbox formatı: minLat,minLon,maxLat,maxLon
                var nasaRegions = new[]
                {
                    ("NorthAmerica", "25,-130,55,-65", "VIIRS_NOAA20_NRT"),   // ABD/Kanada
                    ("SouthAmerica", "-55,-82,15,-34", "VIIRS_NOAA20_NRT"),   // Güney Amerika
                    ("Australia", "-45,110,-10,155", "VIIRS_NOAA20_NRT"),     // Avustralya
                    ("SoutheastAsia", "-10,95,30,145", "VIIRS_NOAA20_NRT"),   // Güneydoğu Asya
                };

                foreach (var (regionName, bbox, source) in nasaRegions)
                {
                    try
                    {
                        _logger.LogDebug("Fetching NASA FIRMS data for {Region}...", regionName);
                        var fires = await _nasaFirmsService.FetchActiveFiresAsync(bbox, 1, source);
                        if (fires.Any())
                        {
                            _logger.LogInformation("NASA {Region}: Found {Count} fires", regionName, fires.Count);
                            allFires.AddRange(fires);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "NASA FIRMS fetch failed for {Region}, continuing...", regionName);
                    }
                }

                if (!allFires.Any())
                {
                    _logger.LogInformation("No new fires found globally");
                    return 0;
                }

                _logger.LogInformation("Total fires received: {Count}", allFires.Count);

                var newFiresCount = 0;
                var duplicateCount = 0;

                foreach (var fire in allFires)
                {
                    var isDuplicate = await IsFireAlreadyExistsAsync(
                        fire.Latitude,
                        fire.Longitude,
                        fire.DetectedAt,
                        fire.Satellite
                    );

                    if (isDuplicate)
                    {
                        duplicateCount++;
                        continue;
                    }

                    await _fireDecetionService.CreateFireDetectionAsync(fire);
                    newFiresCount++;

                    _logger.LogDebug("New fire added: ({Lat}, {Lng}) satellite: {Satellite}",
                        fire.Latitude, fire.Longitude, fire.Satellite);
                }

                // Cache'leri temizle
                await _redisService.SetAsync("last_global_sync", DateTime.UtcNow, TimeSpan.FromDays(1));
                await _redisService.RemoveAsync("active_fires_global");
                await _redisService.RemoveAsync("fire_stats_global");

                _logger.LogInformation("Global sync completed: {New} new fires, {Duplicate} duplicates skipped",
                    newFiresCount, duplicateCount);

                return newFiresCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during global fire sync");
                throw;
            }
        }

        /// <summary>
        /// Duplicate kontrolü - spatial ve temporal proximity
        /// </summary>
        public async Task<bool> IsFireAlreadyExistsAsync(double latitude, double longitude, DateTime detectedAt, string satellite)
        {
            try
            {
                var point = new Point(longitude, latitude) { SRID = 4326 };
                var proximityMeters = 50; // 1km tolerance
                var timeToleranceHours = 6; // 6 saat tolerance

                var startTime = detectedAt.AddHours(-timeToleranceHours);
                var endTime = detectedAt.AddHours(timeToleranceHours);

                var satelliteBase = satellite.Split('-')[0]; // "N-VIIRS" → "N"

                var existingFire = await _fireDetectionDbContext.FireDetections
                    .Where(f => f.Location.IsWithinDistance(point, proximityMeters))
                    .Where(f => f.Satellite == satellite)
                    .Where(f => f.DetectedAt >= startTime && f.DetectedAt <= endTime)
                    .AnyAsync();

                return existingFire;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking fire duplicate for ({Lat}, {Lng})", latitude, longitude);
                return false;
            }
        }

        /// <summary>
        /// Son sync zamanını Redis'ten al
        /// </summary>
        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            try
            {
                return await _redisService.GetAsync<DateTime?>("last_nasa_sync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last sync time");
                return null;
            }
        }
    }
}
