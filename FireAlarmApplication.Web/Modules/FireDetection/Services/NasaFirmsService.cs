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
        public async Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = "36.2,26.0,42.0,43.2", int dayRange = 1)
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

                var response = await _httpClient.GetAsync(endPoint);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response == null)
                {
                    return new List<Models.FireDetection>();
                }

                var fires = ParseCvsResponse(responseContent);

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
                        if (fire != null && IsNearTurkishCity(fire.Latitude, fire.Longitude))
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

            if (parts.Length < 14)
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

                double confidence = 0;
                var confidenceStr = parts[9]; // confidence column
                if (confidenceStr == "n" || confidenceStr == "nominal")
                {
                    confidence = 50; // Default nominal confidence
                }
                else if (confidenceStr == "l" || confidenceStr == "low")
                {
                    confidence = 30; // Low confidence
                }
                else if (confidenceStr == "h" || confidenceStr == "high")
                {
                    confidence = 80; // High confidence
                }
                else
                {
                    double.TryParse(confidenceStr, NumberStyles.Float, CultureInfo.InvariantCulture, out confidence);
                }
                double.TryParse(parts[12], NumberStyles.Float, CultureInfo.InvariantCulture, out var frp); // Fire Radiative Power

                // Parse date/time
                var dateStr = parts[5]; // YYYY-MM-DD
                var timeStr = parts[6]; // HHMM

                if (!DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HHmm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var detectedAt))
                {
                    detectedAt = DateTime.UtcNow; // Fallback
                }

                var satellite = parts[7] ?? "VIIRS";
                var instrument = parts[8] ?? "VIIRS"; // instrument column

                return new Models.FireDetection
                {
                    Id = Guid.NewGuid(),
                    Location = new Point(longitude, latitude) { SRID = 4326 },
                    DetectedAt = detectedAt,
                    Confidence = confidence,
                    Brightness = brightness > 0 ? brightness : null,
                    FireRadiativePower = frp > 0 ? frp : null,
                    Satellite = $"{satellite}-{instrument}", // "N-VIIRS"
                    Status = confidence > 40 ? FireStatus.Verified : FireStatus.Detected,
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

        private bool IsNearTurkishCity(double lat, double lng)
        {
            var turkishCities = new (double lat, double lng, string name)[]
            {
                (41.0082, 28.9784, "İstanbul"), (39.9334, 32.8597, "Ankara"), (38.4192, 27.1287, "İzmir"),
                (36.8969, 30.7133, "Antalya"), (37.0000, 35.3213, "Adana"), (37.8667, 32.4833, "Konya"),
                (40.1885, 29.0610, "Bursa"), (41.2867, 36.3300, "Samsun"), (39.9000, 41.2700, "Erzurum"),
                (38.4891, 43.4089, "Van"), (37.0662, 37.3833, "Gaziantep"), (36.4018, 36.3498, "Hatay"),
                (38.7312, 35.4787, "Kayseri"), (41.0015, 39.7178, "Trabzon"), (36.8000, 34.6333, "Mersin"),
                (37.1674, 38.7955, "Şanlıurfa"), (37.9144, 40.2306, "Diyarbakır")
            };

            const double maxDistanceKm = 150;

            foreach (var city in turkishCities)
            {
                var latDiff = lat - city.lat;
                var lngDiff = lng - city.lng;
                var distance = Math.Sqrt(latDiff * latDiff + lngDiff * lngDiff) * 111; // Rough km conversion

                if (distance <= maxDistanceKm)
                {
                    return true;
                }
            }

            // Safe Turkish interior regions
            if ((lat >= 38.5 && lat <= 40.0 && lng >= 30.5 && lng <= 35.0) || // İç Anadolu
                (lat >= 37.8 && lat <= 40.5 && lng >= 26.0 && lng <= 30.0) || // Batı Anadolu
                (lat >= 40.8 && lat <= 42.0 && lng >= 27.0 && lng <= 40.0))   // Karadeniz
            {
                return true;
            }

            return false;
        }
    }
}
