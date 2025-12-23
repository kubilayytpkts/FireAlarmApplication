using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    /// <summary>
    /// Kullanıcı lokasyon yönetimi ve geofencing işlemleri
    /// User tablosu ile entegre, PostGIS spatial queries kullanır
    /// </summary>
    public class GeofencingService : IGeofencingService
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<GeofencingService> _logger;
        private readonly IDbContextFactory<UserManagementDbContext> _userManagerDbContext;
        private readonly IOsmGeoDataService _osmGeoDataService;

        //redis key prefixes
        private const string USER_LOCATION_KEY = "user_location";
        private const string ACTIVE_USERS_SET = "active_users";
        private const int LOCATION_EXPIRY_MINUTES = 30;

        public GeofencingService(IRedisService redisService, ILogger<GeofencingService> logger, IDbContextFactory<UserManagementDbContext> userManagerDbContext, IOsmGeoDataService osmGeoDataService)
        {
            _redisService = redisService;
            _logger = logger;
            _userManagerDbContext = userManagerDbContext;
            _osmGeoDataService = osmGeoDataService;
        }

        /// <summary>
        /// Belirli koordinat ve yarıçaptaki kullanıcıları bul
        /// UserManagementDbContext kullanarak PostGIS spatial query
        /// </summary>
        public async Task<List<UserLocationInfo>> FindUserInRadiusAsync(double centerLat, double centerLng, double radiusKm)
        {
            try
            {
                var userInRadius = new List<UserLocationInfo>();

                var cacheKey = $"users_in_radius:{centerLat:F2}:{centerLng:F2}:{radiusKm}";

                var cachedUsers = await _redisService.GetAsync<List<UserLocationInfo>>(cacheKey);

                //burası kontrol edilmeli mantıgı uygun olmayabilir 
                if (cachedUsers != null && cachedUsers.Any())
                {
                    _logger.LogDebug("Cache hit: {Count} users found", cachedUsers.Count);
                    return cachedUsers;
                }

                using var context = await _userManagerDbContext.CreateDbContextAsync();
                var centerPoint = new NetTopologySuite.Geometries.Point(centerLng, centerLat) { SRID = 4326 };
                var radiusMeters = radiusKm * 1000;

                var users = await context.Set<User>()
                    .Where(x => x.IsActive && x.IsLocationTrackingEnabled)
                    .Where(x => x.CurrentLocation != null && x.CurrentLocation.IsWithinDistance(centerPoint, radiusMeters) || x.HomeLocation != null && x.CurrentLocation == null && x.HomeLocation.IsWithinDistance(centerPoint, radiusMeters))
                    .Select(user => new
                    {
                        user.Id,
                        user.Role,
                        user.CurrentLocation,
                        user.HomeLocation,
                        user.LastLocationUpdate,
                        user.IsActive,
                    }).ToListAsync();

                foreach (var user in users)
                {
                    var lat = user.CurrentLocation?.Y ?? user.HomeLocation?.Y ?? 0.0;
                    var lng = user.CurrentLocation?.X ?? user.HomeLocation?.X ?? 0.0;


                    var distance = CalculateDistanceKm(lat, lng, centerLat, centerLng);
                    if (distance < radiusKm)
                    {
                        userInRadius.Add(new UserLocationInfo
                        {
                            UserId = user.Id,
                            Latitude = lat,
                            Longitude = lng,
                            UserRole = user.Role,
                            LastUpdated = user.LastLocationUpdate ?? DateTime.UtcNow,
                            IsActive = user.IsActive,
                        });
                    }
                }
                userInRadius = userInRadius.OrderBy(x => CalculateDistanceKm(x.Latitude, x.Longitude, centerLat, centerLng)).ToList();

                if (userInRadius.Any())
                    await _redisService.SetAsync(cacheKey, userInRadius, TimeSpan.FromMinutes(5));
                return userInRadius;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding users in radius");
                return new List<UserLocationInfo>();
            }
        }

        /// <summary>
        /// Kullanıcının aktif konumunu getir
        /// Önce Redis, sonra Database
        /// </summary>
        public async Task<UserLocationInfo?> GetUserLocationInfoAsync(Guid userId)
        {
            try
            {
                var redisKey = @$"{USER_LOCATION_KEY}{userId}";
                var cachedLocation = await _redisService.GetAsync<UserLocationInfo>(redisKey);
                if (cachedLocation != null)
                {
                    _logger.LogDebug("User location found in Redis: {UserId}", userId);
                    return cachedLocation;
                }

                using var context = await _userManagerDbContext.CreateDbContextAsync();
                var user = await context.Set<User>().Where(user => user.Id == userId && user.IsActive)
                    .Select(user => new
                    {
                        user.Id,
                        user.Role,
                        user.CurrentLocation,
                        user.HomeLocation,
                        user.LastLocationUpdate,
                        user.IsLocationTrackingEnabled,
                    }).FirstOrDefaultAsync();

                if (user == null || !user.IsLocationTrackingEnabled)
                {
                    _logger.LogWarning("User {UserId} not found or tracking disabled", userId);
                    return null;
                }

                var locationInfo = new UserLocationInfo
                {
                    UserId = user.Id,
                    Latitude = user.CurrentLocation?.X ?? user.HomeLocation?.X ?? 0,
                    Longitude = user.CurrentLocation?.Y ?? user.HomeLocation?.Y ?? 0,
                    UserRole = user.Role,
                    LastUpdated = user.LastLocationUpdate ?? DateTime.UtcNow,
                    IsActive = true,
                };

                // Geçerli konum varsa cache'e kaydet
                if (locationInfo.Latitude != 0 && locationInfo.Longitude != 0)
                    await _redisService.SetAsync(redisKey, locationInfo, TimeSpan.FromMinutes(LOCATION_EXPIRY_MINUTES));

                return locationInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user location for {UserId}", userId);
                return null;
            }

        }

        /// <summary>
        /// Kullanıcı konumunu güncelle
        /// Hem Redis hem Database'e yaz
        /// </summary>
        public async Task<bool> UpdateUserLocationAsync(Guid userId, double latitude, double longitude)
        {
            try
            {
                var isLocationInTurkey = await _osmGeoDataService.IsUserInTurkey(latitude, longitude);
                if (!isLocationInTurkey)
                {
                    _logger.LogWarning($"Invalid location for Turkey: ({latitude}, {longitude}");
                    return false;
                }

                using var dbcontext = await _userManagerDbContext.CreateDbContextAsync();

                var user = await dbcontext.Set<User>().Where(user => user.Id == userId && user.IsLocationTrackingEnabled).FirstOrDefaultAsync();
                if (user == null)
                {
                    _logger.LogWarning("User not found or location tracking is dissable: {UserId}", userId);
                    return false;
                }

                var newCurrentLocation = new NetTopologySuite.Geometries.Point(latitude, longitude) { SRID = 4326 };
                user.CurrentLocation = newCurrentLocation;
                user.LastLocationUpdate = DateTime.UtcNow;

                await dbcontext.SaveChangesAsync();

                var locationInfo = new UserLocationInfo
                {
                    UserId = userId,
                    Latitude = latitude,
                    Longitude = longitude,
                    UserRole = user.Role,
                    LastUpdated = DateTime.UtcNow,
                    IsActive = true
                };
                var redisKey = $"{USER_LOCATION_KEY}{userId}";
                await _redisService.SetAsync(redisKey, locationInfo, TimeSpan.FromMinutes(LOCATION_EXPIRY_MINUTES));

                // Aktif kullanıcılar setine ekle
                await _redisService.SetAsync($"active_user:{userId}", true, TimeSpan.FromMinutes(LOCATION_EXPIRY_MINUTES));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location for User {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Derece to Radyan dönüşümü
        /// </summary>
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
        public double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371; // Dünya yarıçapı (km)

            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = R * c;

            return Math.Round(distance, 2);
        }
    }
}
