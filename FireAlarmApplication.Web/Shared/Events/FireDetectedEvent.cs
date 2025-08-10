
namespace FireAlarmApplication.Web.Shared.Events;

/// <summary>
/// Yeni yangın tespit edildiğinde fırlatılır
/// Alert module bu event'i dinleyerek yakındaki kullanıcılara uyarı gönderir
/// </summary>
public record FireDetectedEvent(
    /// <summary>Tespit edilen yangının ID'si</summary>
    Guid FireId,

    /// <summary>Enlem koordinatı</summary>
    double Latitude,

    /// <summary>Boylam koordinatı</summary>
    double Longitude,

    /// <summary>NASA FIRMS confidence skoru (0-100)</summary>
    double Confidence,

    /// <summary>AI tarafından hesaplanan risk skoru (0-100)</summary>
    double RiskScore,

    /// <summary>Hangi uydudan geldi (MODIS, VIIRS)</summary>
    string Satellite,

    /// <summary>Tespit zamanı</summary>
    DateTime DetectedAt
) : BaseEvent
{
    /// <summary>Source'u otomatik set et - init-only property</summary>
    public string Source { get; init; } = "FireDetection";
}