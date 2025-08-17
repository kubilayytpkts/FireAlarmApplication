using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class AlertFeedback
    {
        public Guid Id { get; set; }
        public Guid FireAlertId { get; set; }
        public Guid UserId { get; set; }

        // Feedback Content
        public FeedbackType Type { get; set; }
        public string? Comment { get; set; }                       // Opsiyonel kullanıcı yorumu
        public bool IsVerified { get; set; } = false;              // Sistem tarafından doğrulandı mı?

        // User Context (Feedback verirken)
        public double UserLatitude { get; set; }
        public double UserLongitude { get; set; }
        public double DistanceToFireKm { get; set; }

        // Feedback Metadata
        public int ReliabilityScore { get; set; } = 50;            // Kullanıcının güvenirlik puanı (0-100)
        public double ConfidenceImpact { get; set; } = 0;          // Bu feedback confidence'ı ne kadar etkiler

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual FireAlert FireAlert { get; set; } = null!;
        //public virtual User User { get; set; } = null!;
    }
}
