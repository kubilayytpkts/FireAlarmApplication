namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    /// <summary>
    /// NASA FIRMS data sync service
    /// Background job olarak çalışacak - periyodik NASA data sync
    /// </summary>
    public interface IFireDataSyncService
    {
        /// <summary>NASA'dan yeni yangın verilerini çek ve DB'ye sync et</summary>
        Task<int> SyncFiresFromSatellitesAsync();

        /// <summary>Duplicate yangınları kontrol et</summary>
        Task<bool> IsFireAlreadyExistsAsync(double latitude, double longitude, DateTime detectedAt, string satellite);

        /// <summary>Son sync zamanını al</summary>
        Task<DateTime?> GetLastSyncTimeAsync();

        /// <summary>7 günden eski Extinguished/FalsePositive kayıtlarını temizle</summary>
        Task<int> CleanupOldFireDetectionsAsync();
    }
}
