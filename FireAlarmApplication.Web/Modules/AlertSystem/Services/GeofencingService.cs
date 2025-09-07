using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class GeofencingService : IGeofencingService
    {
        public double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            throw new NotImplementedException();
        }

        public Task<List<UserLocationInfo>> FindUserInRadiusAsync(double centerLat, double centerLng, double radiusKm)
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
