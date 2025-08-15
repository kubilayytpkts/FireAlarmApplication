using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Shared.Infrastructure;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class OSMGeoDataService : IOsmGeoDataService
    {
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
        }
        public async Task<bool> IsInForestAreaAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}forest:{lat:F4}:{lng:F4}";
                var cachedResult = await _redisService.GetAsync<bool?>(cacheKey);

                if (cachedResult.HasValue)
                {
                    _logger.LogDebug("🌲 Forest check cache HIT for ({Lat}, {Lng})", lat, lng);
                    return cachedResult.Value;
                }

                _logger.LogDebug("🌲 Forest check cache MISS, querying OSM for ({Lat}, {Lng})", lat, lng);

                var isInForest = await QueryOSMForForest(lat, lng);

                await _redisService.SetAsync(cacheKey, isInForest, TimeSpan.FromHours(CACHE_HOURS));
                return isInForest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking forest for ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }
        public async Task<bool> IsInSettlementAreaAsync(double lat, double lng)
        {
            var cacheKey = $"{CACHE_PREFIX}settlement:{lat:F4}:{lng:F4}";

            try
            {
                var cachedResult = await _redisService.GetAsync<bool?>(cacheKey);
                if (cachedResult.HasValue)
                {
                    _logger.LogDebug("🏘️ Settlement check cache HIT for ({Lat}, {Lng})", lat, lng);
                    return cachedResult.Value;
                }
                var isInSettlement = await QueryOSMForSettlement(lat, lng);

                await _redisService.SetAsync(cacheKey, TimeSpan.FromHours(CACHE_HOURS));
                return isInSettlement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking settlement for ({Lat}, {Lng})", lat, lng);
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
                    return cachedResult.Value;
                }

                var isInProtected = await QueryOSMForProtectedArea(lat, lng);
                await _redisService.SetAsync(cacheKey, isInProtected, TimeSpan.FromHours(CACHE_HOURS));

                return isInProtected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking Protected AreaA for ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }
        public async Task<double> GetDistanceToNearestForestAsync(double lat, double lng)
        {
            try
            {
                var cacheKey = $"{CACHE_PREFIX}forest_distance:{lat:F4}:{lng:F4}";
                var cachedDistance = await _redisService.GetAsync<double?>(cacheKey);

                if (cachedDistance.HasValue) return cachedDistance.Value;

                if (await IsInForestAreaAsync(lat, lng))
                {
                    _logger.LogDebug("🌲📏 Already inside forest: ({Lat}, {Lng})", lat, lng);
                    await _redisService.SetAsync(cacheKey, 0.0, TimeSpan.FromHours(CACHE_HOURS));
                    return 0.0;
                }

                // Yakındaki ormanları bul
                var nearestDistance = await QueryNearestForestDistance(lat, lng);

                await _redisService.SetAsync(cacheKey, nearestDistance, TimeSpan.FromHours(CACHE_HOURS));
                return nearestDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error calculating forest distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue; // Hata durumunda "çok uzak"
            }
        }

        public Task<OSMAreaInfo> GetAreaInfoAsync(double lat, double lng)
        {
            throw new NotImplementedException();
        }


        public Task<double> GetDistanceToNearestSettlementAsync(double lat, double lng)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsServiceHealthyAsync()
        {
            throw new NotImplementedException();
        }

        public Task RefreshAreaDataAsync(string bbox)
        {
            throw new NotImplementedException();
        }

        Task<OSMAreaInfo> IOsmGeoDataService.GetAreaInfoAsync(double lat, double lng)
        {
            throw new NotImplementedException();
        }

        #region HELPER METHODS
        private async Task<bool> QueryOSMForForest(double lat, double lng)
        {
            var forestMetre = 400;

            var query = $@"
                [out:json][timeout:10];
                (
                  way(around:{forestMetre},{lat},{lng})[""landuse""=""forest""];
                  relation(around:{forestMetre},{lat},{lng})[""landuse""=""forest""];
                );
                out count;";
            try
            {
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ OSM API returned {StatusCode} for forest query", response.StatusCode);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);

                bool hasForest = osmResult?.Elements.Any() == true;
                return hasForest;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error querying OSM for forest at ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }
        private async Task<bool> QueryOSMForSettlement(double lat, double lng)
        {
            try
            {
                // Yerleşim alanları için OSM query
                var query = $@"
                        [out:json][timeout:10];
                        (
                          way(around:500,{lat:F6},{lng:F6})[""landuse""~""^(residential|commercial|industrial|retail)$""];
                          relation(around:500,{lat:F6},{lng:F6})[""landuse""~""^(residential|commercial|industrial|retail)$""];
                          node(around:2000,{lat:F6},{lng:F6})[""place""~""^(city|town|village|hamlet)$""];
                        );
                        out count;";

                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ OSM API returned {StatusCode} for settlement query", response.StatusCode);
                    return false;
                }
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);
                bool hasSettlement = osmResult?.Elements?.Any() == true;

                return hasSettlement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error querying OSM for settlement at ({Lat}, {Lng})", lat, lng);
                return false;
            }

        }
        private async Task<bool> QueryOSMForProtectedArea(double lat, double lng)
        {
            var query = $@"
                [out:json][timeout:10];
                (
                  way(around:100,{lat:F6},{lng:F6})[""boundary""=""protected_area""];
                  relation(around:100,{lat:F6},{lng:F6})[""boundary""=""protected_area""];
                  way(around:100,{lat:F6},{lng:F6})[""leisure""=""nature_reserve""];
                  relation(around:100,{lat:F6},{lng:F6})[""leisure""=""nature_reserve""];
                );
                out count;";

            try
            {
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ OSM API returned {StatusCode} for protected area query", response.StatusCode);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(jsonResponse);

                bool hasProtectedArea = osmResult?.Elements?.Any() == true;

                return hasProtectedArea;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error querying OSM for protected area at ({Lat}, {Lng})", lat, lng);
                return false;
            }
        }
        private async Task<double> QueryNearestForestDistance(double lat, double lng)
        {
            try
            {
                var nearForestDistance = 50000;

                var query = $@"
                    [out:json][timeout:15];
                    (
                      way(around:{nearForestDistance},{lat:F6},{lng:F6})[""landuse""=""forest""];
                      relation(around:{nearForestDistance},{lat:F6},{lng:F6})[""landuse""=""forest""];
                      way(around:{nearForestDistance},{lat:F6},{lng:F6})[""natural""=""wood""];
                    );
                    out center meta;";
                var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(query));
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ OSM API returned {StatusCode} for forest distance query", response.StatusCode);
                    return double.MaxValue;
                }
                var responseContent = await response.Content.ReadAsStringAsync();
                var osmResult = System.Text.Json.JsonSerializer.Deserialize<OSMResponse>(responseContent);

                if (osmResult?.Elements == null || !osmResult.Elements.Any())
                {
                    _logger.LogDebug("🌲📏 No forests found within 50km of ({Lat}, {Lng})", lat, lng);
                    return double.MaxValue;
                }

                // en yakın ormanı bul
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
                return minDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error querying nearest forest distance for ({Lat}, {Lng})", lat, lng);
                return double.MaxValue;
            }
        }

        /// <summary>
        /// İki koordinat arası mesafe hesaplama (Haversine formula)
        /// </summary>
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
        #endregion
    }
}
