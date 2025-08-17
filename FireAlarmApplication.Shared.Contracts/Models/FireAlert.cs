using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class FireAlert
    {
        public Guid Id { get; set; }
        public Guid FireDetectionId { get; set; }

        //Alert Content 
        public string Title { get; set; }
        public string Message { get; set; }
        public string LocationDescription { get; set; } = string.Empty; // "Sakarya-Adapazarı arası"

        public AlertSeverity Severity { get; set; }
        public AlertStatus Status { get; set; } = AlertStatus.Active;

        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public double MaxRadiusKm { get; set; }                    // En uzak kullanıcıya mesafe

        // Confidence & Status
        public double OriginalConfidence { get; set; }             // NASA'dan gelen confidence
        public int PositiveFeedbackCount { get; set; } = 0;        // "Evet var" sayısı
        public int NegativeFeedbackCount { get; set; } = 0;        // "Hayır yok" sayısı
        public string? FeedbackSummary { get; set; }               // "2 kullanıcı yangını onayladı"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastFeedbackAt { get; set; }              // Son feedback zamanı
        public DateTime? ResolvedAt { get; set; }
        public DateTime ExpiresAt { get; set; }                   // 24 saat sonra otomatik expire

        // Navigation
        public virtual ICollection<UserAlert> UserAlerts { get; set; } = new List<UserAlert>();
        public virtual ICollection<AlertFeedback> Feedbacks { get; set; } = new List<AlertFeedback>();
    }



}
