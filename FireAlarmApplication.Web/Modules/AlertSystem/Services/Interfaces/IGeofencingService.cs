namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IGeofencingService
    {

        /// <summary>
        /// Belirli kordinat ve yarıçaptaki kullanıcıları bulma
        /// </summary>
        Task<List<UserLocationInfo>> FindUserInRadiusAsync(double centerLat, double centerLng, double radiusKm);

        /// <summary>
        /// iki nokta arası mesafe hesaplama
        /// </summary>
        double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2);

        /// <summary>
        /// kullanıcının aktif konumunu getirma
        /// </summary>
        Task<UserLocationInfo?> GetUserLocationInfoAsync(Guid userId);

        /// <summary>
        /// kullanıcı konumunu güncellema
        /// </summary>
        Task<bool> UpdateUserLocationAsync(Guid userId, double latitude, double longitude);
    }
}
