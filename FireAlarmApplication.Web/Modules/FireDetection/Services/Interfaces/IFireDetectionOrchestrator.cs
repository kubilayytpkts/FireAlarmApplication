namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    public interface IFireDetectionOrchestrator
    {
        Task<Models.FireDetectionResponse> GetFiresForUserLocationAsync(double latitude, double longitude, double radiusKm = 100);
    }
}
