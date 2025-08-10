
namespace FireAlarmApplication.Web.Shared.Events;

/// <summary>
/// Kullanıcıya uyarı oluşturulduğunda fırlatılır
/// Notification module bu event'i dinleyerek email/SMS gönderir
/// </summary>
public record UserAlertCreatedEvent(
    /// <summary>Uyarının ID'si</summary>
    Guid AlertId,

    /// <summary>Uyarı alan kullanıcının ID'si</summary>
    Guid UserId,

    /// <summary>İlgili yangın ID'si</summary>
    Guid FireId,

    /// <summary>Uyarı mesajı</summary>
    string Message,

    /// <summary>Uyarı priority (Low, Medium, High, Critical, Emergency)</summary>
    string Priority,

    /// <summary>Kullanıcının email'i</summary>
    string UserEmail,

    /// <summary>Kullanıcının telefonu (SMS için)</summary>
    string? UserPhone
) : BaseEvent;