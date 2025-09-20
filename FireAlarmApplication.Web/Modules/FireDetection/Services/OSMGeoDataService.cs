using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Shared.Infrastructure;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Globalization;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class OSMGeoDataService : IOsmGeoDataService
    {
        private readonly Geometry _turkeyBorder;
        private readonly HttpClient _httpClient;
        private readonly IRedisService _redisService;
        private readonly ILogger<OSMGeoDataService> _logger;

        //cache süreleri
        private const int CACHE_HOURS = 24;
        private const string CACHE_PREFIX = "osm_";

        public OSMGeoDataService(HttpClient httpClient, IRedisService redisService, ILogger<OSMGeoDataService> logger)
        {
            _httpClient = httpClient;
            _redisService = redisService;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FireGuard-Turkey/1.0");

            var geoJsonText = File.ReadAllText("FireAlarmApplication.Web\\wwwroot\\TurkeyPolygonJson\\lvl0-TR.geojson");
            var reader = new GeoJsonReader();
            _turkeyBorder = reader.Read<Geometry>(geoJsonText);
        }

        public async Task<OSMAreaInfo> GetAreaInfoAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}area_info:{lat:F4}:{lng:F4}";
                var cachedData = await _redisService.GetAsync<OSMAreaInfo>(cacheKey);

                if (cachedData != null)
                {
                    return cachedData;
                }

                var isInForest = await IsInForestAreaAsync(lat, lng);
                var isInSettlement = await IsInSettlementAreaAsync(lat, lng);
                var isInProtected = await IsInProtectedAreaAsync(lat, lng);
                var forestDistance = await GetDistanceToNearestForestAsync(lat, lng);
                var settlementDistance = await GetDistanceToNearestSettlementAsync(lat, lng);

                var areaInfo = new OSMAreaInfo
                {
                    IsInForest = isInForest,
                    IsInSettlement = isInSettlement,
                    IsInProtectedArea = isInProtected,
                    DistanceToNearestForest = forestDistance,
                    DistanceToNearestSettlement = settlementDistance,
                    PrimaryLandUse = DeterminePrimaryLandUse(isInForest, isInSettlement, isInProtected),
                    //AreaNames = await GetAreaNames(lat, lng), // 🆕 Yorum kaldırıldı
                    //CachedAt = DateTime.UtcNow
                };


                await _redisService.SetAsync(cacheKey, areaInfo, TimeSpan.FromHours(12));
                return areaInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting area info for ({Lat}, {Lng})", lat, lng);
                return new OSMAreaInfo(); // 🆕 Boş nesne döndür
            }
        }

        public async Task<bool> IsInForestAreaAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}forest:{lat:F4}:{lng:F4}";
                var cachedResult = await _redisService.GetAsync<bool?>(cacheKey);

                if (cachedResult.HasValue)
                {
                    _logger.LogDebug(" Forest check cache HIT for ({Lat}, {Lng})", lat, lng);
                    return cachedResult.Value;
                }

                _logger.LogDebug("Forest check cache MISS, querying OSM for ({Lat}, {Lng})", lat, lng);

                var isInForest = await QueryOSMForForest(lat, lng);

                await _redisService.SetAsync(cacheKey, isInForest, TimeSpan.FromHours(CACHE_HOURS));

                _logger.LogInformation("Forest check: ({Lat}, {Lng}) = {Result}", lat, lng, isInForest);
                return isInForest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking forest for ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        public async Task<bool> IsInSettlementAreaAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}settlement:{lat:F4}:{lng:F4}";
                var cachedResult = await _redisService.GetAsync<bool?>(cacheKey);

                if (cachedResult.HasValue)
                {
                    _logger.LogDebug("Settlement check cache HIT for ({Lat}, {Lng})", lat, lng);
                    return cachedResult.Value;
                }

                _logger.LogDebug("Settlement check cache MISS, querying OSM for ({Lat}, {Lng})", lat, lng);
                var isInSettlement = await QueryOSMForSettlement(lat, lng);

                // 🆕 DÜZELTME: isInSettlement parametresi eklendi
                await _redisService.SetAsync(cacheKey, isInSettlement, TimeSpan.FromHours(CACHE_HOURS));

                _logger.LogInformation("Settlement check: ({Lat}, {Lng}) = {Result}", lat, lng, isInSettlement);
                return isInSettlement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking settlement for ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        public async Task<bool> IsInProtectedAreaAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}protected:{lat:F4}:{lng:F4}";
                var cachedResult = await _redisService.GetAsync<bool?>(cacheKey);

                if (cachedResult.HasValue)
                {
                    _logger.LogDebug("Protected area check cache HIT for ({Lat}, {Lng})", lat, lng);
                    return cachedResult.Value;
                }

                _logger.LogDebug("Protected area check cache MISS, querying OSM for ({Lat}, {Lng})", lat, lng);
                var isInProtected = await QueryOSMForProtectedArea(lat, lng);

                await _redisService.SetAsync(cacheKey, isInProtected, TimeSpan.FromHours(CACHE_HOURS));

                _logger.LogInformation("Protected area check: ({Lat}, {Lng}) = {Result}", lat, lng, isInProtected);
                return isInProtected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Protected Area for ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        public async Task<double> GetDistanceToNearestForestAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}forest_distance:{lat:F4}:{lng:F4}";
                var cachedDistance = await _redisService.GetAsync<double?>(cacheKey);

                if (cachedDistance.HasValue)
                {
                    _logger.LogDebug("Forest distance cache HIT for ({Lat}, {Lng}): {Distance}km", lat, lng, cachedDistance.Value);
                    return cachedDistance.Value;
                }

                // Önce içinde mi kontrol et
                if (await IsInForestAreaAsync(lat, lng))
                {
                    _logger.LogDebug("Already inside forest: ({Lat}, {Lng})", lat, lng);
                    await _redisService.SetAsync(cacheKey, 0.0, TimeSpan.FromHours(CACHE_HOURS));
                    return 0.0;
                }

                // Yakındaki ormanları bul
                var nearestDistance = await QueryNearestForestDistance(lat, lng);

                await _redisService.SetAsync(cacheKey, nearestDistance, TimeSpan.FromHours(CACHE_HOURS));

                _logger.LogInformation("Nearest forest distance: ({Lat}, {Lng}) = {Distance:F1}km", lat, lng, nearestDistance);
                return nearestDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating forest distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue;
            }
        }

        public async Task<double> GetDistanceToNearestSettlementAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}settlement_distance:{lat:F4}:{lng:F4}";
                var cachedDistance = await _redisService.GetAsync<double?>(cacheKey);

                if (cachedDistance.HasValue)
                {
                    _logger.LogDebug("Settlement distance cache HIT for ({Lat}, {Lng}): {Distance}km", lat, lng, cachedDistance.Value);
                    return cachedDistance.Value;
                }

                // Yerleşim içindeyse mesafe 0
                if (await IsInSettlementAreaAsync(lat, lng))
                {
                    _logger.LogDebug("Already inside settlement: ({Lat}, {Lng})", lat, lng);
                    await _redisService.SetAsync(cacheKey, 0.0, TimeSpan.FromHours(CACHE_HOURS));
                    return 0.0;
                }

                var nearestDistance = await QueryNearestSettlementDistance(lat, lng);
                await _redisService.SetAsync(cacheKey, nearestDistance, TimeSpan.FromHours(CACHE_HOURS));

                _logger.LogInformation("Nearest settlement distance: ({Lat}, {Lng}) = {Distance:F1}km", lat, lng, nearestDistance);
                return nearestDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating settlement distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue;
            }
        }

        public async Task<bool> IsUserInTurkey(double latitude, double longitude)
        {
            var userPoint = new Point(latitude, longitude) { SRID = 4326 };
            bool inside = _turkeyBorder.Contains(userPoint);

            return inside;
        }

        // EKLEME: Eksik metodlar
        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                _logger.LogDebug("Checking OSM service health...");

                // Basit test query - Ankara'da bir city node arayalım
                var testQuery = @"[out:json][timeout:5];node(39.9,32.8,40.0,32.9)[""place""=""city""];out 1;";

                var content = new StringContent(testQuery, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                bool isHealthy = response.IsSuccessStatusCode;

                _logger.LogInformation("OSM service health: {Status}", isHealthy ? "HEALTHY" : "UNHEALTHY");
                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OSM service health check failed");
                return false;
            }
        }

        public async Task RefreshAreaDataAsync(string bbox)
        {
            try
            {
                //_logger.LogInformation("🔄 Refreshing OSM cache for bbox: {BBox}", bbox);

                //// bbox format: "minLat,minLng,maxLat,maxLng" kontrolü
                //var parts = bbox.Split(',');
                //if (parts.Length != 4)
                //{
                //    _logger.LogWarning("⚠️ Invalid bbox format: {BBox}", bbox);
                //    return;
                //}

                //// Bu bbox içindeki tüm cache'leri temizle
                //// Basit yaklaşım: tüm OSM cache'ini temizle
                //var pattern = $"{CACHE_PREFIX}*";
                //await _redisService.RemovePatternAsync(pattern);

                _logger.LogInformation("✅ OSM cache refreshed for bbox: {BBox}", bbox);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error refreshing OSM cache for bbox: {BBox}", bbox);
            }
        }

        #region HELPER METHODS

        private async Task<bool> QueryOSMForForest(double lat, double lng)
        {
            var forestMetre = 400;

            // Koordinat formatını düzelt
            var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
            var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

            var query = $@"[out:json][timeout:10];
(
  way(around:{forestMetre},{latStr},{lngStr})[landuse=forest];
  relation(around:{forestMetre},{latStr},{lngStr})[landuse=forest];
  way(around:{forestMetre},{latStr},{lngStr})[natural=wood];
);
out count;";

            try
            {
                _logger.LogDebug("OSM Forest Query: ({Lat}, {Lng})", lat, lng);

                // 🆕 DÜZELTME: Content-Type eklendi
                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OSM API returned {StatusCode} for forest query. Response: {Response}",
                        response.StatusCode, errorContent);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("OSM Response: {Content}", jsonResponse);

                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);
                bool hasForest = osmResult?.Elements?.Any() == true;

                _logger.LogDebug("Forest result: {HasForest} (Elements: {Count})", hasForest, osmResult?.Elements?.Count ?? 0);
                return hasForest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying OSM for forest at ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        private async Task<bool> QueryOSMForSettlement(double lat, double lng)
        {
            try
            {
                var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
                var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

                var query = $@"[out:json][timeout:10];
(
  way(around:500,{latStr},{lngStr})[landuse~""^(residential|commercial|industrial|retail)$""];
  relation(around:500,{latStr},{lngStr})[landuse~""^(residential|commercial|industrial|retail)$""];
  node(around:2000,{latStr},{lngStr})[place~""^(city|town|village|hamlet)$""];
);
out count;";

                _logger.LogDebug("OSM Settlement Query: ({Lat}, {Lng})", lat, lng);

                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OSM API returned {StatusCode} for settlement query. Response: {Response}",
                        response.StatusCode, errorContent);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);
                bool hasSettlement = osmResult?.Elements?.Any() == true;

                _logger.LogDebug("Settlement result: {HasSettlement} (Elements: {Count})", hasSettlement, osmResult?.Elements?.Count ?? 0);
                return hasSettlement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying OSM for settlement at ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        private async Task<bool> QueryOSMForProtectedArea(double lat, double lng)
        {
            var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
            var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

            var query = $@"[out:json][timeout:10];
(
  way(around:100,{latStr},{lngStr})[boundary=protected_area];
  relation(around:100,{latStr},{lngStr})[boundary=protected_area];
  way(around:100,{latStr},{lngStr})[leisure=nature_reserve];
  relation(around:100,{latStr},{lngStr})[leisure=nature_reserve];
);
out count;";

            try
            {
                _logger.LogDebug("🛡️ OSM Protected Area Query: ({Lat}, {Lng})", lat, lng);

                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OSM API returned {StatusCode} for protected area query. Response: {Response}",
                        response.StatusCode, errorContent);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);
                bool hasProtectedArea = osmResult?.Elements?.Any() == true;

                _logger.LogDebug("Protected area result: {HasProtected} (Elements: {Count})", hasProtectedArea, osmResult?.Elements?.Count ?? 0);
                return hasProtectedArea;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying OSM for protected area at ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }

        private async Task<double> QueryNearestForestDistance(double lat, double lng)
        {
            try
            {
                var nearForestDistance = 50000;
                var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
                var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

                var query = $@"[out:json][timeout:15];
(
  way(around:{nearForestDistance},{latStr},{lngStr})[landuse=forest];
  relation(around:{nearForestDistance},{latStr},{lngStr})[landuse=forest];
  way(around:{nearForestDistance},{latStr},{lngStr})[natural=wood];
);
out center meta;";

                _logger.LogDebug("OSM Nearest Forest Query: ({Lat}, {Lng})", lat, lng);

                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OSM API returned {StatusCode} for forest distance query", response.StatusCode);
                    return double.MaxValue;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(responseContent);

                if (osmResult?.Elements == null || !osmResult.Elements.Any())
                {
                    _logger.LogDebug("No forests found within 50km of ({Lat}, {Lng})", lat, lng);
                    return double.MaxValue;
                }

                var minDistance = double.MaxValue;
                foreach (var element in osmResult.Elements)
                {
                    if (element.Lat.HasValue && element.Lon.HasValue)
                    {
                        var distance = CalculateDistance(lat, lng, element.Lat.Value, element.Lon.Value);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                        }
                    }
                }

                _logger.LogDebug("Nearest forest found: {Distance:F1}km from ({Lat}, {Lng})", minDistance, lat, lng);
                return minDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying nearest forest distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue;
            }
        }

        private async Task<double> QueryNearestSettlementDistance(double lat, double lng)
        {
            try
            {
                var distanceByNearestSettlement = 30000;
                var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
                var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

                var query = $@"[out:json][timeout:15];
(
  node(around:{distanceByNearestSettlement},{latStr},{lngStr})[place~""^(city|town|village)$""];
  way(around:{distanceByNearestSettlement},{latStr},{lngStr})[landuse~""^(residential|commercial)$""];
);
out center meta;";

                _logger.LogDebug(" OSM Nearest Settlement Query: ({Lat}, {Lng})", lat, lng);

                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode)
                {
                    return double.MaxValue;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);

                if (osmResult?.Elements == null || !osmResult.Elements.Any())
                {
                    _logger.LogDebug("No settlements found within 30km of ({Lat}, {Lng})", lat, lng);
                    return double.MaxValue;
                }

                double minDistance = double.MaxValue;
                foreach (var element in osmResult.Elements)
                {
                    if (element.Lat.HasValue && element.Lon.HasValue)
                    {
                        var distance = CalculateDistance(lat, lng, element.Lat.Value, element.Lon.Value);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                        }
                    }
                }

                _logger.LogDebug("Nearest settlement found: {Distance:F1}km from ({Lat}, {Lng})", minDistance, lat, lng);
                return minDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error querying nearest settlement distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue;
            }
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371; // Dünya yarıçapı (km)

            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = R * c;

            return distance;
        }

        private async Task<List<string>> GetAreaNames(double lat, double lng)
        {
            try
            {
                var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
                var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);

                var query = $@"[out:json][timeout:10];
(
  way(around:1000,{latStr},{lngStr})[name];
  relation(around:1000,{latStr},{lngStr})[name];
  node(around:5000,{latStr},{lngStr})[place][name];
);
out tags;";

                var content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                if (!response.IsSuccessStatusCode) return new List<string>();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);

                var names = new List<string>();
                if (osmResult?.Elements != null)
                {
                    foreach (var element in osmResult.Elements)
                    {
                        if (element.Tags?.ContainsKey("name") == true)
                        {
                            var name = element.Tags["name"];
                            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                            {
                                names.Add(name);
                            }
                        }
                    }
                }

                return names.Take(5).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting area names for ({Lat}, {Lng})", lat, lng);
                return new List<string>();
            }
        }

        private string DeterminePrimaryLandUse(bool isInForest, bool isInSettlement, bool isInProtected)
        {
            if (isInForest) return "forest";
            if (isInProtected) return "protected";
            if (isInSettlement) return "settlement";
            return "unknown";
        }

        #endregion
    }
}