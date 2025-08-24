using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    /// <summary>
    /// Kullanıcı tipi ve konum bazlı alert kuralları
    /// </summary>
    public class AlertRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;           // "Orman Görevlisi Kuralı"
        public string Description { get; set; } = string.Empty;

        public UserRole TargetUserRole { get; set; }
        public double MinConfidence { get; set; } // Minimum confidence threshold
        public double MaxDistanceKm { get; set; }  // Maksimum mesafe
        public bool AllowFeedback { get; set; } = true; // Feedback alınabilir mi?

        //Message Templates
        public string TitleTemplate { get; set; } = string.Empty; // "Yangın Tespiti - {Location}"
        public string MessageTemplate { get; set; } = string.Empty;

        //Rule Status
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public static List<AlertRule> GetDefaultRules()
        {
            return new List<AlertRule>
            {
                new AlertRule
                {
                    Id = 1,
                    Name = "Vatandaş - Yakın Mesafe",
                    Description = "Vatandaşlar için yakın yangın uyarıları",
                    TargetUserRole = UserRole.Civilian,
                    MinConfidence = 50,
                    MaxDistanceKm = 15,
                    AllowFeedback = true,
                    TitleTemplate = "DİKKAT: Yangın Tespiti - {Location}",
                    MessageTemplate = "{Distance}km uzağınızda yangın tespit edildi. Güvenirlik: %{Confidence}. Dikkatli olun."
                },
                new AlertRule
                {
                    Id = 2,
                    Name = "Orman Görevlisi - Geniş Alan",
                    Description = "Orman görevlileri için tüm şüpheli tespitler",
                    TargetUserRole = UserRole.ForestOfficer,
                    MinConfidence = 30,
                    MaxDistanceKm = 50,
                    AllowFeedback = false,
                    TitleTemplate = "Şüpheli Yangın Tespiti - {Location}",
                    MessageTemplate = "{Distance}km mesafede şüpheli yangın tespiti. Güvenirlik: %{Confidence}. Kontrol edilmesi gerekiyor."
                },
                new AlertRule
                {
                    Id = 3,
                    Name = "İtfaiye - Acil Müdahale",
                    Description = "İtfaiye için yüksek güvenirlikli yangınlar",
                    TargetUserRole = UserRole.FireDepartment,
                    MinConfidence = 60,
                    MaxDistanceKm = 100,
                    AllowFeedback = false,
                    TitleTemplate = "ACİL: Yangın Müdahale - {Location}",
                    MessageTemplate = "{Distance}km mesafede yangın tespit edildi. Güvenirlik: %{Confidence}. Acil müdahale gerekli."
                }
            };

        }
    }
}
