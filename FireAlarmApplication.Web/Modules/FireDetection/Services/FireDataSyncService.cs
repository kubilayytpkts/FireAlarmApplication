using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.FireDetection.Data;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class FireDataSyncService : IFireDataSyncService
    {
        private readonly IFireDetectionService _fireDetectionService;
        private readonly INasaFirmsService _nasaFirmsService;
        private readonly IMtgFireService _mtgFireService;
        private readonly FireDetectionDbContext _fireDetectionDbContext;
        private readonly IRedisService _redisService;
        private readonly ILogger<FireDataSyncService> _logger;

        /// <summary>
        /// NASA FIRMS icin bolgesel sync - MTG kapsami disindaki bolgeler
        /// MTG zaten Avrupa/Afrika/Orta Dogu'yu tek seferde kapsiyor
        /// </summary>
        private static readonly (string Name, double CenterLat, double CenterLon, double RadiusKm)[] NasaRegions = new[]
        {
            ("NorthAmerica",  40.0, -100.0, 2500.0),
            ("SouthAmerica", -15.0,  -60.0, 2500.0),
            ("Australia",    -25.0,  135.0, 1500.0),
            ("SoutheastAsia", 10.0,  110.0, 1500.0),
            ("EastAsia",      35.0,  120.0, 1500.0),
        };

        public FireDataSyncService(
            IFireDetectionService fireDetectionService,
            INasaFirmsService nasaFirmsService,
            IMtgFireService mtgFireService,
            FireDetectionDbContext fireDetectionDbContext,
            IRedisService redisService,
            ILogger<FireDataSyncService> logger)
        {
            _fireDetectionService = fireDetectionService;
            _nasaFirmsService = nasaFirmsService;
            _mtgFireService = mtgFireService;
            _fireDetectionDbContext = fireDetectionDbContext;
            _redisService = redisService;
            _logger = logger;
        }

        /// <summary>
        /// Global veri senkronizasyonu
        /// 1) MTG - tek seferde tum kapsama alani (Avrupa, Afrika, Orta Dogu, Turkiye)
        ///    Full Disk urun indirip bbox olmadan parse eder, land mask + confidence filtreler
        /// 2) NASA FIRMS - bolgesel (Amerika, Asya, Okyanusya)
        ///    Her bolge icin ayri API cagrisi yapar
        /// </summary>
        public async Task<int> SyncFiresFromSatellitesAsync()
        {
            try
            {
                _logger.LogInformation("Starting global fire data sync...");
                var allFires = new List<Models.FireDetection>();

                // =============================================
                // 1) MTG - Tek seferde tum kapsama alani
                // Avrupa, Afrika, Orta Dogu, Turkiye
                // Full Disk urun zaten tum bolgeyi kapsiyor
                // bbox gondermeye gerek yok, land mask + confidence filtreler
                // =============================================
                try
                {
                    _logger.LogInformation("Fetching MTG fires (Europe/Africa/Middle East)...");
                    var mtgFires = await _mtgFireService.FetchActiveFiresAsync(area: null, minutesRange: 1440);

                    if (mtgFires.Any())
                    {
                        _logger.LogInformation("MTG: Found {Count} fires", mtgFires.Count);
                        allFires.AddRange(mtgFires);
                    }
                    else
                    {
                        _logger.LogInformation("MTG: No fires found");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MTG fetch failed, continuing with NASA...");
                }

                // =============================================
                // 2) NASA FIRMS - Bolgesel
                // MTG kapsami disindaki bolgeler: Amerika, Asya, Okyanusya
                // Her bolge icin ayri bbox ile API cagrisi
                // =============================================
                _logger.LogInformation("Fetching NASA fires ({RegionCount} regions)...", NasaRegions.Length);

                foreach (var (regionName, centerLat, centerLon, radiusKm) in NasaRegions)
                {
                    try
                    {
                        var bbox = CalculateBBox(centerLat, centerLon, radiusKm);

                        _logger.LogDebug("Syncing {Region}: bbox=({MinLon},{MinLat},{MaxLon},{MaxLat})",
                            regionName, bbox.minLon, bbox.minLat, bbox.maxLon, bbox.maxLat);

                        var fires = await FetchFromNASA(bbox, "VIIRS_NOAA20_NRT");

                        if (fires.Any())
                        {
                            _logger.LogInformation("NASA {Region}: Found {Count} fires", regionName, fires.Count);
                            allFires.AddRange(fires);
                        }
                        else
                        {
                            _logger.LogDebug("NASA {Region}: No fires found", regionName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "NASA fetch failed for {Region}, continuing...", regionName);
                    }
                }

                // =============================================
                // 3) DB'ye kaydet (duplicate kontrolu ile)
                // =============================================
                if (!allFires.Any())
                {
                    _logger.LogInformation("No fires found from any source");
                    return 0;
                }

                _logger.LogInformation("Total fires from all sources: {Count}. Saving to DB...", allFires.Count);

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

                    await _fireDetectionService.CreateFireDetectionAsync(fire);
                    newFiresCount++;
                }

                // Cache temizle
                await _redisService.SetAsync("last_global_sync", DateTime.UtcNow, TimeSpan.FromDays(1));
                await _redisService.RemoveAsync("active_fires_turkey");
                await _redisService.RemoveAsync("fire_stats_turkey");

                _logger.LogInformation("Sync completed: {New} new, {Duplicate} duplicates, {Total} total",
                    newFiresCount, duplicateCount, allFires.Count);

                return newFiresCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during global fire sync");
                throw;
            }
        }

        // ============================================
        // SATELLITE FETCH METHODS
        // ============================================

        /// <summary>
        /// NASA FIRMS'den veri cek
        /// NASA API bbox formati: W,S,E,N = minLon,minLat,maxLon,maxLat
        /// </summary>
        private async Task<List<Models.FireDetection>> FetchFromNASA(
            (double minLat, double minLon, double maxLat, double maxLon) bbox,
            string source)
        {
            // NASA formati: W,S,E,N = minLon,minLat,maxLon,maxLat
            var bboxString = FormattableString.Invariant(
                $"{bbox.minLon},{bbox.minLat},{bbox.maxLon},{bbox.maxLat}");

            return await _nasaFirmsService.FetchActiveFiresAsync(
                area: bboxString,
                dayRange: 1,
                source: source);
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Merkez koordinat + yaricap'tan bbox hesapla
        /// </summary>
        private (double minLat, double minLon, double maxLat, double maxLon) CalculateBBox(
            double lat, double lon, double radiusKm)
        {
            var latDelta = radiusKm / 111.0;
            var lonDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

            return (
                minLat: lat - latDelta,
                minLon: lon - lonDelta,
                maxLat: lat + latDelta,
                maxLon: lon + lonDelta
            );
        }

        /// <summary>
        /// Duplicate kontrolu - spatial ve temporal proximity
        /// 1km yaricap, 6 saat zaman toleransi, ayni uydu
        /// </summary>
        public async Task<bool> IsFireAlreadyExistsAsync(double latitude, double longitude, DateTime detectedAt, string satellite)
        {
            try
            {
                var point = new Point(longitude, latitude) { SRID = 4326 };
                var proximityMeters = 1000;
                // geometry(Point,4326) derece cinsinden calisir
                var proximityDegrees = proximityMeters / 111_320.0;
                var timeToleranceHours = 6;

                var startTime = detectedAt.AddHours(-timeToleranceHours);
                var endTime = detectedAt.AddHours(timeToleranceHours);

                var existingFire = await _fireDetectionDbContext.FireDetections
                    .Where(f => f.Location.IsWithinDistance(point, proximityDegrees))
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
        /// Son sync zamanini Redis'ten al
        /// </summary>
        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            try
            {
                return await _redisService.GetAsync<DateTime?>("last_global_sync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last sync time");
                return null;
            }
        }

        /// <summary>
        /// 7 gunden eski Extinguished/FalsePositive kayitlarini temizle
        /// </summary>
        public async Task<int> CleanupOldFireDetectionsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-7);

                var oldRecords = await _fireDetectionDbContext.FireDetections
                    .Where(f => f.DetectedAt < cutoffDate)
                    .Where(f => f.Status == FireStatus.Extinguished
                             || f.Status == FireStatus.FalsePositive)
                    .ToListAsync();

                if (!oldRecords.Any())
                {
                    _logger.LogDebug("No old fire detections to cleanup");
                    return 0;
                }

                _fireDetectionDbContext.FireDetections.RemoveRange(oldRecords);
                await _fireDetectionDbContext.SaveChangesAsync();

                await _redisService.RemoveAsync("active_fires_turkey");
                await _redisService.RemoveAsync("fire_stats_turkey");

                _logger.LogInformation("Cleaned up {Count} old fire detection records (older than {Date})",
                    oldRecords.Count, cutoffDate);

                return oldRecords.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old fire detections");
                return 0;
            }
        }
    }
}