namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    /// <summary>
    /// NASA FIRMS API integration service
    /// Real fire data fetching
    /// </summary>
    public interface INasaFirmsService
    {
        /// <summary>NASA FIRMS'den aktif yangınları çek</summary>
        Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = "35.8,25.7,42.1,44.8", int dayRange = 10, string source = "");

        /// <summary>NASA FIRMS API health check</summary>
        Task<bool> IsApiHealthyAsync();

        /// <summary>Specific region için yangınları çek</summary>
        Task<List<Models.FireDetection>> FetchFiresForRegionAsync(double minLat, double minLng, double maxLat, double maxLng, int dayRange = 1);
    }
}
