using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IUserAlertService
    {
        /// <summary>
        /// Alert için uygun kullanıcıları bul ve useralert oluştur
        /// </summary>
        Task<List<UserAlert>> CreateUserAlertsAsync(Guid);

        /// <summary>
        /// Kullanıcının aktif alertlerini getir
        /// </summary>
        Task<List<UserAlert>> GetUserAlertsAsync(Guid userId, bool onlyUnread = false);


        /// <summary>
        /// UserAlerti okundu olarak işaretle
        /// </summary>
        Task<bool> MarkAsReadAsync(Guid fireAlertId, Guid userId);


        /// <summary>
        /// kullanıcı alert alabilir mi kontrolü
        /// </summary>
        Task<bool> ShouldReceiveAlertAsync(Guid userId, double fireLatitude, double fireLongitude, double confidence);

        /// <summary>
        /// Kullanıcı için özel alert mesajı oluştur
        /// </summary>
        Task<string> GenerateUserSpesificMessageAsync(Guid userId, UserRole userRole, double distance, double confidence);

    }
}
