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

        //redis key prefixes
        private const string USER_LOCATION_KEY = "user_location";
        private const string ACTIVE_USERS_SET = "active_users";
        private const int LOCATION_EXPIRY_MINUTES = 30;

        public GeofencingService(IRedisService redisService, ILogger<GeofencingService> logger, IDbContextFactory<UserManagementDbContext> userManagerDbContext)
        {
            _redisService = redisService;
            _logger = logger;
            _userManagerDbContext = userManagerDbContext;
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

        public double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            throw new NotImplementedException();
        }

        public Task<UserLocationInfo?> GetUserLocationInfoAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateUserLocationAsync(Guid userId, double latitude, double longitude)
        {
            throw new NotImplementedException();
        }
    }
}
