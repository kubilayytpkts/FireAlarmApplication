using FireAlarmApplication.Web.Modules.FireDetection.Models;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    /// <summary>
    /// Fire Detection service interface
    /// Core business logic operations
    /// </summary>
    public interface IFireDetectionService
    {
        /// <summary>Aktif yangınları getir</summary>
        Task<List<FireDto>> GetActiveFiresAsync();

        /// <summary>Belirli konum yakınındaki yangınları getir</summary>
        Task<List<FireDto>> GetFiresNearLocationAsync(double latitude, double longitude, double radiusKm);

        /// <summary>Yangın istatistiklerini getir</summary>
        Task<FireStatsDto> GetFireStatsAsync();

        /// <summary>Yeni yangın kaydı oluştur</summary>
        Task<FireDto> CreateFireDetectionAsync(Models.FireDetection fireDetection);

        /// <summary>Yangın durumunu güncelle</summary>
        Task<FireDto?> UpdateFireStatusAsync(Guid fireId, FireAlarmApplication.Shared.Contracts.Enums.FireStatus status);

        /// <summary>Risk skorunu güncelle</summary>
        Task<FireDto?> UpdateRiskScoreAsync(Guid fireId, double riskScore);
    }
}
