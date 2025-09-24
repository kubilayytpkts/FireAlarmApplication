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
        private readonly FireDetectionDbContext _fireDetectionDbContext;
        private readonly IRedisService _redisService;
        private readonly ILogger<FireDataSyncService> _logger;
        private readonly NasaFirmsOptions _options;
        public FireDataSyncService(IFireDetectionService fireDecetionService, INasaFirmsService nasaFirmsService, FireDetectionDbContext fireDetectionDbContext
            , IRedisService redisService, ILogger<FireDataSyncService> logger, IOptions<NasaFirmsOptions> options)
        {
            _fireDecetionService = fireDecetionService;
            _nasaFirmsService = nasaFirmsService;
            _fireDetectionDbContext = fireDetectionDbContext;
            _redisService = redisService;
            _logger = logger;
            _options = options.Value;
        }
        /// <summary>
        /// NASA'dan veri çek ve database'e sync et
        /// </summary>
        public async Task<int> SyncFiresFromNasaAsync()
        {
            try
            {
                _logger.LogInformation("Starting NASA FIRMS data sync...");

                var nasaFires = await _nasaFirmsService.FetchActiveFiresAsync(_options.DefaultArea, _options.DefaultDayRange);

                if (!nasaFires.Any())
                {
                    _logger.LogInformation("No new fires from NASA FIRMS");
                    return 0;
                }

                _logger.LogInformation("Received {Count} fires from NASA FIRMS", nasaFires.Count);

                var newFiresCount = 0;
                var duplicateCount = 0;

                foreach (var nasaFire in nasaFires)
                {
                    //var isDuplicate = await IsFireAlreadyExistsAsync(
                    //nasaFire.Latitude,
                    //nasaFire.Longitude,
                    //nasaFire.DetectedAt,
                    //nasaFire.Satellite
                    // );

                    //if (isDuplicate)
                    //{
                    //    duplicateCount++;
                    //    _logger.LogDebug("Duplicate fire skipped: ({Lat}, {Lng}) from {Satellite}", nasaFire.Latitude, nasaFire.Longitude, nasaFire.Satellite);
                    //    continue;
                    //}
                    await _fireDecetionService.CreateFireDetectionAsync(nasaFire);
                    newFiresCount++;

                    _logger.LogDebug("New fire added: ({Lat}, {Lng}) confidence: {Confidence}%", nasaFire.Latitude, nasaFire.Longitude, nasaFire.Confidence);
                }
                // Son sync zamanını kaydet
                await _redisService.SetAsync("last_nasa_async", DateTime.UtcNow, TimeSpan.FromDays(1));

                await _redisService.RemoveAsync("active_fires_turkey");
                await _redisService.RemoveAsync("fire_stats_turkey");

                _logger.LogInformation("NASA sync completed: {New} new fires, {Duplicate} duplicates skipped", newFiresCount, duplicateCount);

                return newFiresCount;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing fires from NASA FIRMS");
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
