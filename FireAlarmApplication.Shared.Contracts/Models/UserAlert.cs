using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class UserAlert
    {
        public Guid Id { get; set; }
        public Guid FireAlertId { get; set; }
        public Guid UserId { get; set; }                           // User entity reference

        // User Context (Alert oluşturulduğu andaki kullanıcı durumu)
        public UserRole UserRole { get; set; }
        public double UserLatitude { get; set; }                   // Kullanıcı konumu (o an)
        public double UserLongitude { get; set; }
        public double DistanceToFireKm { get; set; }               // Yangına mesafe

        //alert delivery
        public string AlertMessage { get; set; } = string.Empty;  // Kullanıcıya Özel Mesaj
        public bool CanProvideFeedBack { get; set; } // Kullanıcı Feedback verebilir mi ? 
        public bool IsDelivered { get; set; } = false; // Bildirim ulaştı mı ?
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; } // Kullanıcı Okudu mu ?

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual FireAlert FireAlert { get; set; } = null!;

    }
}
