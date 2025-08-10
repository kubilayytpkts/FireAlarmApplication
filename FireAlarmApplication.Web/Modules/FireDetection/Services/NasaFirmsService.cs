using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using System.Globalization;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class NasaFirmsService : INasaFirmsService
    {
        private readonly HttpClient _httpClient;
        private readonly FireGuardOptions _fireGuardOptions;
        private readonly ILogger<NasaFirmsService> _logger;

        public NasaFirmsService(HttpClient httpClient, IOptions<FireGuardOptions> fireGuardOptions, ILogger<NasaFirmsService> logger)
        {
            _httpClient = httpClient;
            _fireGuardOptions = fireGuardOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// NASA FIRMS'den aktif yangınları çek (Turkey bounds)
        /// </summary>
        public async Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = "36,26,42,45", int dayRange = 1)
        {
            try
            {
                var apiKey = _fireGuardOptions.NasaFirms.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("⚠️ NASA FIRMS API key not configured");
                    return new List<Models.FireDetection>();
                }

                var endPoint = $"api/area/csv/{apiKey}/VIIRS_SNPP_NRT/{area}/{dayRange}";

                _logger.LogDebug("🌍 Fetching fires from NASA FIRMS: {Endpoint}", endPoint);

                var response = await _httpClient.GetAsync(endPoint);

                if (response == null)
                {
                    _logger.LogWarning("⚠️ Empty response from NASA FIRMS API");
                    return new List<Models.FireDetection>();
                }

                var fires = ParseCvsResponse(response.Content.ToString());
                _logger.LogInformation("🔥 Fetched {Count} fires from NASA FIRMS", fires.Count);

                return fires;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching active fires from NASA FIRMS");

                throw;
            }
        }

        /// <summary>
        /// Specific region için yangınları çek
        /// </summary>
        public async Task<List<Models.FireDetection>> FetchFiresForRegionAsync(double minLat, double minLng, double maxLat, double maxLng, int dayRange = 1)
        {
            try
            {
                var area = $"{minLat},{minLng},{maxLat},{maxLng}";
                return await FetchActiveFiresAsync(area, dayRange);

            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// NASA FIRMS API health check
        /// </summary>
        public async Task<bool> IsApiHealthyAsync()
        {
            try
            {
                var testArea = "39,32,40,33";
                var endpoint = $"api/area/csv/{_fireGuardOptions.NasaFirms.ApiKey}/VIIRS_SNPP_NRT/{testArea}/1";

                var response = await _httpClient.GetAsync(endpoint);
                var isHealthy = response.IsSuccessStatusCode;
                _logger.LogDebug("🏥 NASA FIRMS API health: {Status}", isHealthy ? "Healthy" : "Unhealthy");

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ NASA FIRMS API health check failed");
                throw;
            }
        }

        /// <summary>
        /// NASA FIRMS CSV response'unu parse et
        /// </summary>
        private List<Models.FireDetection> ParseCvsResponse(string csvContent)
        {
            var fires = new List<Models.FireDetection>();
            try
            {
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length <= 1)
                {
                    _logger.LogWarning("⚠️ No data lines in CSV response");
                    return fires;
                }


                for (int i = 0; i < lines.Length; i++)
                {
                    try
                    {
                        var fire = ParseCsvLine(lines[i]);
                        if (fire != null)
                        {
                            fires.Add(fire);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error parsing CSV line {Line}: {Content}", i, lines[i]);
                    }
                }
                return fires;
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// Single CSV line'ı FireDetection entity'ye çevir
        /// NASA FIRMS CSV format: latitude,longitude,brightness,scan,track,acq_date,acq_time,satellite,confidence,version,bright_t31,frp,daynight
        /// </summary>
        private Models.FireDetection? ParseCsvLine(string csvLine)
        {
            var parts = csvLine.Split(',');

            if (parts.Length < 13)
            {
                return null;
            }

            try
            {
                // Parse coordinates
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                {
                    return null;
                }

                // Turkey bounds check
                if (latitude < 35 || latitude > 43 || longitude < 25 || longitude > 46)
                {
                    return null; // Outside Turkey
                }

                // Parse other fields
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var brightness);
                double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence);
                double.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out var frp);

                // Parse date/time
                var dateStr = parts[5]; // YYYY-MM-DD
                var timeStr = parts[6]; // HHMM

                if (!DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HHmm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var detectedAt))
                {
                    detectedAt = DateTime.UtcNow; // Fallback
                }

                var satellite = parts[7] ?? "VIIRS";

                return new Models.FireDetection
                {
                    Id = Guid.NewGuid(),
                    Location = new Point(longitude, latitude) { SRID = 4326 },
                    DetectedAt = detectedAt,
                    Confidence = confidence,
                    Brightness = brightness > 0 ? brightness : null,
                    FireRadiativePower = frp > 0 ? frp : null,
                    Satellite = satellite,
                    Status = confidence > 50 ? FireStatus.Verified : FireStatus.Detected,
                    RiskScore = 0, // Will be calculated separately
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error parsing fire data from line: {Line}", csvLine);
                return null;
            }
        }
    }
}
