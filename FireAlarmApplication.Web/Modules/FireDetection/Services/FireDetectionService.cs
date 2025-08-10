using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.FireDetection.Data;
using FireAlarmApplication.Web.Modules.FireDetection.Models;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Events;
using FireAlarmApplication.Web.Shared.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Point = NetTopologySuite.Geometries.Point;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class FireDetectionService : IFireDetectionService
    {
        private readonly FireDetectionDbContext _context;
        private readonly IRedisService _redis;
        private readonly IMediator _mediator;
        private readonly ILogger<FireDetectionService> _logger;
        public FireDetectionService(FireDetectionDbContext context, IRedisService redis, IMediator mediator, ILogger<FireDetectionService> logger)
        {
            _context = context;
            _redis = redis;
            _mediator = mediator;
            _logger = logger;
        }
        /// <summary>
        /// Aktif yangınları getir (cache'li)
        /// </summary>
        public async Task<List<FireDto>> GetActiveFiresAsync()
        {
            const string cachekey = "active_fires_turkey";

            try
            {
                var cachedFires = await _redis.GetAsync<List<FireDto>>(cachekey);
                if (cachedFires != null)
                {
                    _logger.LogDebug($"✅ Active fires loaded from cache: {cachedFires.Count} fires");
                    return cachedFires;
                }

                var fires = await _context.FireDetections
                    .Where(f => f.Status == FireStatus.Active || f.Status == FireStatus.Verified)
                    .Where(f => f.DetectedAt > DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(f => f.DetectedAt)
                    .Take(100)
                    .Select(f => MapToDto(f))
                    .ToListAsync();

                await _redis.SetAsync(cachekey, fires, TimeSpan.FromMinutes(5));

                _logger.LogInformation($"🔥 Active fires loaded from database: {fires.Count} fires");
                return fires;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting active fires");

                throw;
            }
        }
        /// <summary>
        /// Belirli konum yakınındaki yangınları getir (spatial query)
        /// </summary>
        public async Task<List<FireDto>> GetFiresNearLocationAsync(double latitude, double longitude, double radiusKm)
        {
            try
            {
                var point = new Point(longitude, latitude) { SRID = 4326 };
                var radiusMeters = radiusKm * 1000;

                var nearbyFires = await _context.FireDetections
                    .Where(f => f.Location.IsWithinDistance(point, radiusMeters))
                    .Where(f => f.DetectedAt > DateTime.UtcNow.AddDays(-7))
                    .OrderBy(f => f.Location.Distance(point))
                    .Take(50)
                    .Select(f => MapToDto(f))
                    .ToListAsync();

                _logger.LogInformation("🎯 Found {Count} fires within {Radius}km of ({Lat}, {Lng})", nearbyFires.Count, radiusKm, latitude, longitude);

                return nearbyFires;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting nearby fires for location ({Lat}, {Lng})", latitude, longitude);
                throw;
            }
        }
        public async Task<FireStatsDto> GetFireStatsAsync()
        {
            const string cacheKey = "fire_stats_turkey";
            try
            {
                var cachedStats = await _redis.GetAsync<FireStatsDto>(cacheKey);
                if (cachedStats != null)
                {
                    return cachedStats;
                }

                var last7Days = DateTime.UtcNow.AddDays(-7);

                var stats = new FireStatsDto
                {
                    TotalFires = await _context.FireDetections
                    .Where(f => f.DetectedAt > last7Days)
                     .CountAsync(),

                    ActiveFires = await _context.FireDetections
                    .Where(f => f.Status == FireStatus.Active && f.DetectedAt > DateTime.UtcNow.AddHours(-24))
                     .CountAsync(),

                    ExtinguishedFires = await _context.FireDetections
                    .Where(f => f.Status == FireStatus.Extinguished && f.DetectedAt > last7Days)
                     .CountAsync(),

                    FalsePositives = await _context.FireDetections
                    .Where(f => f.Status == FireStatus.FalsePositive && f.DetectedAt > last7Days)
                     .CountAsync(),

                    AverageRiskScore = await _context.FireDetections
                     .Where(f => f.DetectedAt > last7Days && f.RiskScore > 0)
                      .AverageAsync(f => (double)f.RiskScore),

                    LastDetection = await _context.FireDetections
                     .OrderByDescending(f => f.DetectedAt)
                      .Select(f => f.DetectedAt)
                       .FirstOrDefaultAsync(),

                    FiresByStatus = await _context.FireDetections
                    .Where(f => f.DetectedAt > last7Days)
                     .GroupBy(f => f.Status)
                      .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count()),

                    FiresBySatellite = await _context.FireDetections
                    .Where(f => f.DetectedAt > last7Days)
                     .GroupBy(f => f.Satellite)
                      .ToDictionaryAsync(g => g.Key, g => g.Count())
                };
                await _redis.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(10));

                _logger.LogInformation("📊 Fire stats calculated: {Total} total, {Active} active",
                      stats.TotalFires, stats.ActiveFires);

                return stats;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating fire stats");
                throw;
            }
        }

        /// <summary>
        /// Yeni yangın kaydı oluştur ve event fırlat
        /// </summary>
        public async Task<FireDto> CreateFireDetectionAsync(Models.FireDetection fireDetection)
        {
            try
            {
                _context.FireDetections.Add(fireDetection);
                await _context.SaveChangesAsync();

                await _redis.RemoveAsync("active_fires_turkey");
                await _redis.RemoveAsync("fire_stats_turkey");

                var fireEvent = new FireDetectedEvent(

                    fireDetection.Id,
                    fireDetection.Latitude,
                    fireDetection.Longitude,
                    fireDetection.Confidence,
                    fireDetection.RiskScore,
                    fireDetection.Satellite,
                    fireDetection.DetectedAt
                );
                await _mediator.Publish(fireEvent);

                _logger.LogInformation("🔥 New fire created: {FireId} at ({Lat}, {Lng}) from {Satellite}",
                    fireDetection.Id, fireDetection.Latitude, fireDetection.Longitude, fireDetection.Satellite);

                return MapToDto(fireDetection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating fire detection");
                throw;
            }
        }
        /// <summary>
        /// Yangın durumunu güncelle
        /// </summary>

        public async Task<FireDto?> UpdateFireStatusAsync(Guid fireId, FireStatus status)
        {
            try
            {
                var fire = await _context.FireDetections.FindAsync(fireId);
                if (fire == null)
                {
                    _logger.LogWarning("⚠️ Fire not found for status update: {FireId}", fireId);
                    return null;
                }

                var oldStatus = fire.Status;
                fire.Status = status;
                await _context.SaveChangesAsync();

                await _redis.RemoveAsync("active_fires_turkey");
                await _redis.RemoveAsync("fire_stats_turkey");

                _logger.LogInformation("🔄 Fire status updated: {FireId} from {OldStatus} to {NewStatus}", fireId, oldStatus, status);

                return MapToDto(fire);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating fire status for {FireId}", fireId);
                throw;
            }
        }

        /// <summary>
        /// Risk skorunu güncelle
        /// </summary>
        public async Task<FireDto?> UpdateRiskScoreAsync(Guid fireId, double riskScore)
        {
            try
            {
                var fire = await _context.FireDetections.FindAsync(fireId);
                if (fire == null)
                {
                    return null;
                }

                fire.RiskScore = Math.Clamp(riskScore, 0, 100);

                _logger.LogDebug("📊 Risk score updated: {FireId} -> {RiskScore}", fireId, riskScore);

                return MapToDto(fire);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating risk score for {FireId}", fireId);
                throw;
            }
        }

        /// <summary>
        /// Entity'yi DTO'ya çevir
        /// </summary>
        private static FireDto MapToDto(Models.FireDetection fire)
        {
            return new FireDto
            {
                Id = fire.Id,
                Latitude = fire.Latitude,
                Longitude = fire.Longitude,
                DetectedAt = fire.DetectedAt,
                Confidence = fire.Confidence,
                Brightness = fire.Brightness,
                FireRadiativePower = fire.FireRadiativePower,
                Satellite = fire.Satellite,
                Status = fire.Status.ToString(),
                RiskScore = fire.RiskScore,
                RiskCategory = fire.RiskCategory,
                IsActive = fire.IsActive,
                Age = fire.Age,
                CreatedAt = fire.CreatedAt,
                UpdatedAt = fire.UpdatedAt
            };
        }
    }
}
