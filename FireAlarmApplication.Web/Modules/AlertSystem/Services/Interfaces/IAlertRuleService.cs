using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IAlertRuleService
    {
        /// <summary>
        /// kullanıcı rolüne göre geçerli alert kuralını getir
        /// </summary>
        Task<AlertRule?> GetRuleForUserRoleAsync(UserRole userRole);

        /// <summary>
        /// tüm aktif kuralları getir
        /// </summary>
        Task<List<AlertRule>> GetActiveRulesAsync();

        /// <summary>
        /// kural oluştur veya güncelle
        /// </summary>
        Task<AlertRule> CreateOrUpdateRuleAsync(AlertRule alertRule);

        /// <summary>
        /// Templateden mesaj oluştur
        /// </summary>
        string GenerateMessageFromTemplate(string template, Dictionary<string, object> placeHolders);


        /// <summary>
        /// kullanıcı + yangın kombinasyonu için uygun kuralı bul
        /// </summary>
        Task<AlertRule?> FindApplicableRuleAsync(UserRole userRole, double distance, double confidence);

    }
}
