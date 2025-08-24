using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IAlertService
    {
        /// <summary>
        /// Yangın tespiti için alert oluştur
        /// </summary>
        Task<FireAlert> CreateFireAlertAsync(Guid fireDetectionId, double confidence, double latitude, double longitude);

        /// <summary>
        /// Aktif alertleri getir
        /// </summary>
        Task<List<FireAlert>> GetActiveAlertsAsync();

        /// <summary>
        /// Belirli bir alert'i getir
        /// </summary>
        Task<FireAlert?> GetAlertByIdAsync(Guid alertId);

        /// <summary>
        /// Alert Durumunu Güncelle
        /// </summary>
        Task<bool> UpdateAlertStatusAsync(Guid alertId, AlertStatus status);

        /// <summary>
        /// Süresi dolmuş alertleri temizle
        /// </summary>
        Task<int> CleanupExpiredAlertsAsync();

        /// <summary>
        /// Alert için mesaj içeriğini genarete et
        /// </summary>
        Task<(string title, string message)> GenerateAlertContentAsync(double confidence, double latitude, double longitude);

    }
}
