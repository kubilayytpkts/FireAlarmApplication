namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    public interface IMtgFireService
    {
        Task<List<Models.FireDetection>> FetchActiveFiresAsync(string area = null, int minutesRange = 30);
        Task<List<Models.FireDetection>> FetchFiresForRegionAsync(double minLat, double minLng, double maxLat, double maxLng, int minutesRange = 30);
        Task<bool> IsApiHealthyAsync();
    }
}
