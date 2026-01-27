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
        private readonly IRiskCalculationService _riskCalculationService;

        public NasaFirmsService(HttpClient httpClient, IOptions<FireGuardOptions> fireGuardOptions, ILogger<NasaFirmsService> logger, IRiskCalculationService riskCalculationService)
        {
            _httpClient = httpClient;
            _fireGuardOptions = fireGuardOptions.Value;
            _logger = logger;
            _riskCalculationService = riskCalculationService;
        }

        /// <summary>
        /// NASA FIRMS'den aktif yangınları çek VIIRS + MODIS
        /// </summary>
        /// NASA FIRMS API bbox formatı: minLat,minLon,maxLat,maxLon
        /// Türkiye sınırları: Lat 36-42, Lon 26-45
        public async Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = "36,26,42,45", int dayRange = 1, string source = "")
        {
            try
            {

                var apiKey = _fireGuardOptions.NasaFirms.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("NASA FIRMS API key not configured");
                    return new List<Models.FireDetection>();
                }

                var endPoint = $"api/area/csv/{apiKey}/{source}/{area}/{dayRange}";

                var response = await _httpClient.GetAsync(endPoint);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response == null)
                {
                    return new List<Models.FireDetection>();
                }

                var fires = await ParseCvsResponse(responseContent);

                return fires;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active fires from NASA FIRMS");

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
                _logger.LogDebug("NASA FIRMS API health: {Status}", isHealthy ? "Healthy" : "Unhealthy");

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NASA FIRMS API health check failed");
                throw;
            }
        }

        /// <summary>
        /// NASA FIRMS CSV response'unu parse et
        /// </summary>
        private async Task<List<Models.FireDetection?>> ParseCvsResponse(string csvContent)
        {
            var fires = new List<Models.FireDetection>();
            try
            {
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length <= 1)
                {
                    _logger.LogWarning("No data lines in CSV response");
                    return fires;
                }

                // İlk satır header, atla (latitude,longitude,bright_ti4,scan,track,...)
                for (int i = 1; i < lines.Length; i++)
                {
                    var fire = await ParseCsvLine(lines[i]);
                    if (fire != null)
                        fires.Add(fire);
                }

                return fires;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing CSV line {Line}: {Content}", null);
                return new List<Models.FireDetection?>();
            }
        }

        /// <summary>
        /// Single CSV line'ı FireDetection entity'ye çevir
        /// NASA FIRMS CSV format: latitude,longitude,brightness,scan,track,acq_date,acq_time,satellite,confidence,version,bright_t31,frp,daynight
        /// </summary>
        private async Task<Models.FireDetection> ParseCsvLine(string csvLine)
        {
            var parts = csvLine.Split(',');
            if (parts.Length < 14) return null;

            try
            {
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                    return null;

                // Global uygulama: Koordinat geçerlilik kontrolü
                // Geçerli koordinat aralığı: Lat -90 to 90, Lon -180 to 180
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                    return null;

                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var brightness);

                double confidence = 0;
                var confidenceStr = parts[9];
                if (confidenceStr == "n" || confidenceStr == "nominal") confidence = 50; // Default nominal confidence
                else if (confidenceStr == "l" || confidenceStr == "low") confidence = 30; // Low confidence
                else if (confidenceStr == "h" || confidenceStr == "high") confidence = 80; // High confidence
                else double.TryParse(confidenceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out confidence);

                double.TryParse(parts[12], NumberStyles.Float, CultureInfo.InvariantCulture, out var frp); // Fire Radiative Power

                var dateStr = parts[5];
                var timeStr = parts[6];

                if (!DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var detectedAt))
                {
                    detectedAt = DateTime.UtcNow;
                }

                var satellite = parts[7] ?? "VIIRS";
                var instrument = parts[8] ?? "VIIRS";

                var fire = new Models.FireDetection
                {
                    Id = Guid.NewGuid(),
                    Location = new Point(longitude, latitude) { SRID = 4326 },
                    DetectedAt = detectedAt,
                    Confidence = confidence,
                    Brightness = brightness > 0 ? brightness : null,
                    FireRadiativePower = frp > 0 ? frp : null,
                    Satellite = $"{satellite}-{instrument}", // "N-VIIRS"
                    Status = confidence > 40 ? FireStatus.Verified : FireStatus.Detected,
                    RiskScore = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                //fire.RiskScore = await _riskCalculationService.CalculateRiskScoreAsync(fire);

                return fire;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing fire data from line: {Line}", csvLine);
                return null;
            }
        }
    }
}
