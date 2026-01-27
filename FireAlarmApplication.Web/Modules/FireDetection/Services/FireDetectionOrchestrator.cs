using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Modules.FireDetection.Models;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using System.Globalization;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class FireDetectionOrchestrator : IFireDetectionOrchestrator
    {
        private readonly IMtgFireService _mtgService;
        private readonly INasaFirmsService _nasaService;
        private readonly IRegionDetectionService _regionService;
        private readonly ILogger<FireDetectionOrchestrator> _logger;

        public FireDetectionOrchestrator(
        IMtgFireService mtgService,
        INasaFirmsService nasaService,
        IRegionDetectionService regionService,
        ILogger<FireDetectionOrchestrator> logger)
        {
            _mtgService = mtgService;
            _nasaService = nasaService;
            _regionService = regionService;
            _logger = logger;
        }

        public async Task<FireDetectionResponse> GetFiresForUserLocationAsync(double latitude, double longitude, double radiusKm = 1000)
        {
            List<Models.FireDetection> fires;

            try
            {
                var sourceInfo = _regionService.GetFastestSource(latitude, longitude);

                switch (sourceInfo.Source)
                {
                    case SatelliteSource.MTG:
                        fires = await FetchFromMTG(latitude, longitude, radiusKm);
                        break;

                    case SatelliteSource.VIIRS_Realtime:
                        fires = await FetchFromVIIRSRealtime(latitude, longitude, radiusKm);
                        break;

                    case SatelliteSource.VIIRS_Standard:
                        fires = await FetchFromVIIRSStandard(latitude, longitude, radiusKm);
                        break;

                    case SatelliteSource.MODIS:
                        fires = await FetchFromMODIS(latitude, longitude, radiusKm);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown satellite source: {sourceInfo.Source}");
                }

                var nearbyFires = fires
                .Select(f => new
                {
                    Fire = f,
                    Distance = CalculateDistance(latitude, longitude, f.Latitude, f.Longitude)
                })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Select(x => new FireDetectionDto
                {
                    Id = x.Fire.Id,
                    Latitude = x.Fire.Latitude,
                    Longitude = x.Fire.Longitude,
                    DistanceKm = Math.Round(x.Distance, 2),
                    DetectedAt = x.Fire.DetectedAt,
                    Satellite = x.Fire.Satellite,
                    Confidence = x.Fire.Confidence,
                    Status = x.Fire.Status.ToString(),
                    AgeMinutes = (int)(DateTime.UtcNow - x.Fire.DetectedAt).TotalMinutes
                })
                .ToList();


                return new FireDetectionResponse
                {
                    SourceInfo = sourceInfo,
                    UserLocation = new UserLocation
                    {
                        Latitude = latitude,
                        Longitude = longitude
                    },
                    RadiusKm = radiusKm,
                    FireCount = nearbyFires.Count,
                    Fires = nearbyFires
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from {Source}, trying fallback");

                // FALLBACK: Hata olursa VIIRS Standard kullan
                fires = await FetchFromVIIRSStandard(latitude, longitude, radiusKm);
                var sourceInfo = new SatelliteSourceInfo
                {
                    Source = SatelliteSource.VIIRS_Standard,
                    Name = "VIIRS (Fallback)",
                    LatencyMinutes = 270,
                    Region = "Global"
                };

                throw ex;
            }
        }

        // ============================================
        // SATELLITE-SPECIFIC FETCH METHODS
        // ============================================

        private async Task<List<Models.FireDetection>> FetchFromMTG(double lat, double lon, double radiusKm)
        {
            //_logger.LogInformation("Fetching from MTG (10-20 min latency)...");

            var bbox = CalculateBBox(lat, lon, radiusKm);
            var bboxString = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F6},{1:F6},{2:F6},{3:F6}",
                bbox.minLat,
                bbox.minLon,
                bbox.maxLat,
                bbox.maxLon
            );

            return await _mtgService.FetchActiveFiresAsync(area: bboxString, minutesRange: 1440);
        }

        private async Task<List<Models.FireDetection>> FetchFromVIIRSRealtime(double lat, double lon, double radiusKm)
        {
            //_logger.LogInformation("Fetching from VIIRS Real-time (2-4 hour latency)...");

            var bbox = CalculateBBox(lat, lon, radiusKm);
            //var bboxString = $"{bbox.minLat},{bbox.minLon},{bbox.maxLat},{bbox.maxLon}";
            var bboxString = FormattableString.Invariant($"{bbox.minLon},{bbox.minLat},{bbox.maxLon},{bbox.maxLat}");

            return await _nasaService.FetchActiveFiresAsync(
                area: bboxString,
                dayRange: 1,
                source: "VIIRS_NOAA20_NRT");
        }

        private async Task<List<Models.FireDetection>> FetchFromVIIRSStandard(double lat, double lon, double radiusKm)
        {
            //_logger.LogInformation("Fetching from VIIRS Standard (3-6 hour latency)...");

            var bbox = CalculateBBox(lat, lon, radiusKm);
            var bboxString = FormattableString.Invariant($"{bbox.minLon},{bbox.minLat},{bbox.maxLon},{bbox.maxLat}");

            return await _nasaService.FetchActiveFiresAsync(
                area: bboxString,
                dayRange: 1,
                source: "VIIRS_NOAA20_NRT");
        }

        private async Task<List<Models.FireDetection>> FetchFromMODIS(double lat, double lon, double radiusKm)
        {
            //_logger.LogInformation("Fetching from MODIS (3-6 hour latency)...");

            var bbox = CalculateBBox(lat, lon, radiusKm);
            //var bboxString = $"{bbox.minLat},{bbox.minLon},{bbox.maxLat},{bbox.maxLon}";
            var bboxString = FormattableString.Invariant($"{bbox.minLon},{bbox.minLat},{bbox.maxLon},{bbox.maxLat}");


            return await _nasaService.FetchActiveFiresAsync(
                area: bboxString,
                dayRange: 1,
                source: "MODIS_NRT");
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private (double minLat, double minLon, double maxLat, double maxLon) CalculateBBox(double lat, double lon, double radiusKm)
        {
            // 1 derece latitude ≈ 111 km
            // 1 derece longitude ≈ 111 km * cos(latitude)
            var latDelta = radiusKm / 111.0;
            var lonDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

            return (
                minLat: lat - latDelta,
                minLon: lon - lonDelta,
                maxLat: lat + latDelta,
                maxLon: lon + lonDelta
            );
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula
            var R = 6371; // Earth radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        private double ToRadians(double degrees) => degrees * Math.PI / 180;

    }
}
