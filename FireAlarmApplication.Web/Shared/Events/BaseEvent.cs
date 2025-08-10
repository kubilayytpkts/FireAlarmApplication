using MediatR;

namespace FireAlarmApplication.Web.Shared.Events
{
    /// <summary>
    /// Tüm domain event'lerin base class'ı
    /// MediatR INotification interface'ini implement eder
    /// Her event unique ID ve timestamp ile birlikte gelir
    /// </summary>
    public abstract record BaseEvent : INotification
    {
        /// <summary>Her event'in unique identifier'ı</summary>
        public Guid EventId { get; init; } = Guid.NewGuid();

        /// <summary>Event'in oluşturulma zamanı (UTC)</summary>
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

        /// <summary>Event'i oluşturan service/module adı</summary>
        public string Source { get; init; } = string.Empty;
    }
}
