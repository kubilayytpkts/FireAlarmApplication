namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IGeofencingService
    {

        /// <summary>
        /// Belirli kordinat ve yarıçaptaki kullanıcıları bul
        /// </summary>
        Task<List<UserLocationInfo>> FindUserInRadiusAsync(double centerLat, double centerLng, double radiusKm);

        /// <summary>
        /// iki nokta arası mesafe hesapla
        /// </summary>
        double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2);

        /// <summary>
        /// kullanıcının aktif konumunu getir
        /// </summary>
        Task<UserLocationInfo?> GetUserLocationInfoAsync(Guid userId);

        /// <summary>
        /// kullanıcı konumunu güncelle
        /// </summary>
        Task<bool> UpdateUserLocationAsync(Guid userId, double latitude, double longitude);
    }
}
