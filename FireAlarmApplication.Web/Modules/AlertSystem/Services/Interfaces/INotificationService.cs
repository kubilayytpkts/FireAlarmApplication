using FireAlarmApplication.Shared.Contracts.Models;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// UserAlertler için push notification gönder
        /// </summary>
        Task<bool> SendPushNotificationAsync(UserAlert userAlert);

        /// <summary>
        /// Email notification gönder
        /// </summary>
        Task<bool> SendEmailNotificationAsync(UserAlert userAlert);

        /// <summary>
        /// Sms notification gönder
        /// </summary>
        Task<bool> SendSmsNotificationAsync(UserAlert userAlert);

        /// <summary>
        /// kullanıcı tercihine göre uygun kanaldan bildirim gönder
        /// </summary>
        Task<bool> SendNotificationAsync(UserAlert userAlert);

        /// <summary>
        /// toplu bildirim gönder
        /// </summary>
        Task<Dictionary<Guid, bool>> SendBatchNotificationAsync(List<UserAlert> userAlerts);

        /// <summary>
        /// bildirim gönderimini işaretle
        /// </summary>
        Task<bool> MarkAsDeliveridAsync(Guid userAlertId);
    }
}
