using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    /// <summary>
    /// Yangın alertlerini yöneten servis
    /// FireAlert entity'lerini oluşturur ve yönetir
    /// </summary>
    public class AlertService : IAlertService
    {
        private readonly AlertSystemDbContext _context;
        private readonly IRedisService _redisService;
        private readonly ILogger<AlertService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string fireAlertCacheKey = "active_fire_alerts";
        public AlertService(AlertSystemDbContext context, IRedisService redisService, ILogger<AlertService> logger, HttpClient httpClient)
        {
            _context = context;
            _redisService = redisService;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<FireAlert> CreateFireAlertAsync(Guid fireDetectionId, double confidence, double latitude, double longitude)
        {
            try
            {
                var existingAlert = _context.FireAlerts
                     .Where(a => a.FireDetectionId == fireDetectionId)
                     .Where(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Confirmed)
                     .FirstOrDefault();

                if (existingAlert != null)
                {
                    _logger.LogWarning("Alert already exists for fire {FireId}", fireDetectionId);
                    return existingAlert;
                }

                //Alert content oluşturma
                var (title, message) = await GenerateAlertContentAsync(confidence, latitude, longitude);
                var locationDescription = await GetLocationDescriptionAsync(latitude, longitude);
                var severity = CalculateSeverity(confidence);

                //Fire Alert entity oluşturma
                var fireAlert = new FireAlert
                {
                    Id = new Guid(),
                    FireDetectionId = fireDetectionId,
                    Title = title,
                    Message = message,
                    LocationDescription = locationDescription,
                    Severity = severity,
                    Status = AlertStatus.Active,
                    CenterLatitude = latitude,
                    CenterLongitude = longitude,
                    MaxRadiusKm = DetermineAlertRadius(severity),
                    OriginalConfidence = confidence,
                    PositiveFeedbackCount = 0,
                    NegativeFeedbackCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                _context.FireAlerts.Add(fireAlert);
                await _context.SaveChangesAsync();

                await InvalidateAlertCacheAsync();
                return fireAlert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fire alert for detection {FireId}", fireDetectionId);
                throw;
            }
        }


        /// <summary>
        /// Aktif alertleri getir (cache'li)
        /// </summary>
        public async Task<List<FireAlert>> GetActiveAlertsAsync()
        {
            try
            {
                var cachedAlerts = await _redisService.GetAsync<List<FireAlert>>(fireAlertCacheKey);
                if (cachedAlerts != null)
                {
                    return cachedAlerts;
                }

                var activeAlerts = await _context.FireAlerts
                    .Include(a => a.UserAlerts)
                    .Include(a => a.Feedbacks)
                    .Where(x => x.Status == AlertStatus.Active || x.Status == AlertStatus.Confirmed)
                    .OrderByDescending(x => x.Severity)
                    .ThenByDescending(x => x.CreatedAt)
                    .Take(100)
                    .ToListAsync();

                await _redisService.SetAsync(fireAlertCacheKey, activeAlerts, TimeSpan.FromMinutes(5));

                _logger.LogDebug("Retrieved {Count} active alerts", activeAlerts.Count);
                return activeAlerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alerts");
                throw;
            }
        }


        /// <summary>
        /// Süresi dolmuş alertleri temizle
        /// Hangfire job olarak çalıştırılacak
        /// </summary>
        public async Task<int> CleanupExpiredAlertsAsync()
        {
            try
            {
                var expiredAlerts = await _context.FireAlerts
                    .Where(x => x.ExpiresAt < DateTime.UtcNow)
                    .Where(x => x.Status != AlertStatus.Expired)
                    .ToListAsync();

                if (!expiredAlerts.Any())
                {
                    return 0;
                }
                foreach (var alert in expiredAlerts)
                {
                    alert.Status = AlertStatus.Expired;
                }

                await _context.SaveChangesAsync();
                await InvalidateAlertCacheAsync();

                return expiredAlerts.Count;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired alerts");
                return 0;
            }
        }

        public async Task<(string title, string message)> GenerateAlertContentAsync(double confidence, double latitude, double longitude)
        {
            try
            {
                var severity = CalculateSeverity(confidence);
                var location = await GetLocationDescriptionAsync(latitude, longitude);

                var title = severity switch
                {
                    AlertSeverity.Critical => $"ACİL: Yangın Tespiti - {location}",
                    AlertSeverity.High => $"Yüksek Risk: Yangın Tespiti - {location}",
                    AlertSeverity.Medium => $"Orta Risk: Yangın Tespiti - {location}",
                    AlertSeverity.Low => $"Düşük Risk: Yangın Uyarısı - {location}",
                    _ => $"Yangın İzleme - {location}"
                };

                var message = $"Koordinat: {latitude:F4}°, {longitude:F4}° | " +
                                 $"Güvenirlik: %{confidence:F0} | " +
                                 $"Risk Seviyesi: {severity} | " +
                                 $"Tespit Zamanı: {DateTime.UtcNow:HH:mm}";

                return (title, message);

            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// Belirli bir alert'i getir
        /// </summary>
        public async Task<FireAlert?> GetAlertByIdAsync(Guid alertId)
        {
            try
            {
                var alert = await _context.FireAlerts
                    .Include(i => i.UserAlerts)
                    .Include(i => i.Feedbacks)
                    .FirstOrDefaultAsync();

                if (alert == null)
                    _logger.LogWarning("Alert not found: {AlertId}", alertId);

                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alert {AlertId}", alertId);
                throw;
            }
        }

        /// <summary>
        /// Alert durumunu güncelle
        /// </summary>
        public async Task<bool> UpdateAlertStatusAsync(Guid alertId, AlertStatus status)
        {
            try
            {
                var alert = await _context.FireAlerts.FindAsync(alertId);
                if (alert == null)
                    return false;

                var oldStatus = alert.Status;
                alert.Status = status;

                // Resolved ise resolved time'ı set et
                if (status == AlertStatus.Resolved && !alert.ResolvedAt.HasValue)
                {
                    alert.ResolvedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await InvalidateAlertCacheAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert status for {AlertId}", alertId);
                return false;
            }
        }

        public async Task<string> GetLocationDescriptionAsync(double latitude, double longitude)
        {
            try
            {
                //Cache key
                var cacheKey = $"location_desc:{latitude:F4}:{longitude:F4}";
                var cached = await _redisService.GetAsync<string>(cacheKey);

                if (!string.IsNullOrEmpty(cached)) return cached;

                // Nominatim API çağrısı
                var url = $"https://nominatim.openstreetmap.org/reverse?" +
            $"lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&format=json&zoom=16&addressdetails=1&accept-language=tr";


                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "FireGuard-Turkey/1.0");

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response).RootElement;

                var displayName = json.TryGetProperty("display_name", out var dn) ? dn.GetString() : "";

                var description = !string.IsNullOrEmpty(displayName) ? displayName + " yakınları" : $"{latitude:F4}°, {longitude:F4}° yakınları";

                await _redisService.SetAsync(cacheKey, description);

                return description;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get location description, using coordinates");
                return $"{latitude:F4}°N, {longitude:F4}°E";
            }
        }

        #region HELPER METHODS
        /// <summary>
        /// Confidence'a göre severity hesaplama
        /// </summary>
        private AlertSeverity CalculateSeverity(double confidence)
        {
            return confidence switch
            {
                >= 85 => AlertSeverity.Critical,
                >= 70 => AlertSeverity.High,
                >= 55 => AlertSeverity.Medium,
                >= 40 => AlertSeverity.Low,
                _ => AlertSeverity.Info
            };
        }


        /// <summary>
        /// Severity'ye göre alert radius belirleme
        /// </summary>
        private double DetermineAlertRadius(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical => 50.0,  // 50 km
                AlertSeverity.High => 30.0,      // 30 km
                AlertSeverity.Medium => 20.0,    // 20 km
                AlertSeverity.Low => 10.0,       // 10 km
                AlertSeverity.Info => 5.0,       // 5 km
                _ => 10.0
            };
        }

        private async Task InvalidateAlertCacheAsync()
        {
            try
            {
                await _redisService.RemoveAsync(fireAlertCacheKey);
                _logger.LogDebug("Alert cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not invalidate alert cache");
            }
        }

        #endregion
    }
}
