using FireAlarmApplication.Shared.Contracts.Enums;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = new Guid();

        //Temel bilgiler
        [Required][MaxLength(255)] public string Email { get; set; }
        [MaxLength(20)] public string PhoneNumber { get; set; }
        [Required][MaxLength(100)] public string FirstName { get; set; }
        [Required][MaxLength(100)] public string LastName { get; set; }
        public UserRole Role { get; set; } = UserRole.Civilian;

        //Konum bilgileri
        public Point? HomeLocation { get; set; } // SRID 4326 - Ev/sabit konum
        public Point? CurrentLocation { get; set; }// SRID 4326 - Güncel konum
        public DateTime? LastLocationUpdate { get; set; }
        public int LocationUpdateFrequencyMinutes { get; set; } = 30; // Varsayılan 30 dakika
        public bool IsLocationTrackingEnabled { get; set; } = true;
        public double LocationAccuracy { get; set; } = 0; // Metre cinsinden GPS accuracy

        //Bildirim tercihleri
        //public double NotificationRadiusKm { get; set; } = 20.0; // Varsayılan 20km
        public bool EnableSmsNotification { get; set; } = false;
        public bool EnableEmailNotification { get; set; } = true;
        public bool EnablePushNotification { get; set; } = true;
        //public TimeOnly? QuietHoursStart { get; set; } // Rahatsız etme başlangıç
        //public TimeOnly? QuietHoursEnd { get; set; } // Rahatsız etme bitiş

        //auth bilgileri
        public string PasswordHash { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        //Push notification
        [MaxLength(500)] public string? FcmToken { get; set; }
        [MaxLength(500)] public string? ApnsToken { get; set; }

        //Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public bool IsEmailVerified { get; set; } = false;

        // Computed properties
        public string FullName => $"{FirstName} {LastName}";
        public double? Latitude => CurrentLocation?.Y ?? HomeLocation?.Y;
        public double? Longitude => CurrentLocation?.X ?? HomeLocation?.X;
    }
}
