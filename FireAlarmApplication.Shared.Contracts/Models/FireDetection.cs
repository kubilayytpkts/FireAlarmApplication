using FireAlarmApplication.Shared.Contracts.Enums;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace FireAlarmApplication.Web.Modules.FireDetection.Models
{
    /// <summary>
    /// Yangın tespit kaydı - NASA FIRMS'den gelen data
    /// PostGIS Point kullanarak coğrafi konum saklar
    /// </summary>
    public class FireDetection
    {
        /// <summary>Primary key</summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Coğrafi konum (PostGIS Point)
        /// SRID 4326 (WGS84) coordinate system
        /// </summary>
        [Required]
        public Point Location { get; set; } = null!;

        /// <summary>NASA FIRMS tespit zamanı (UTC)</summary>
        [Required]
        public DateTime DetectedAt { get; set; }

        /// <summary>NASA FIRMS confidence skoru (0-100)</summary>
        [Range(0, 100)]
        public double Confidence { get; set; }

        /// <summary>Brightness temperature (T4 channel)</summary>
        public double? Brightness { get; set; }

        /// <summary>Fire Radiative Power (MW)</summary>
        public double? FireRadiativePower { get; set; }

        /// <summary>Uydu adı (MODIS, VIIRS, GOES)</summary>
        [MaxLength(50)]
        public string Satellite { get; set; } = string.Empty;

        /// <summary>Yangın durumu</summary>
        public FireStatus Status { get; set; } = FireStatus.Detected;

        /// <summary>AI tarafından hesaplanan risk skoru (0-100)</summary>
        [Range(0, 100)]
        public double RiskScore { get; set; } = 0;

        /// <summary>Kayıt oluşturulma zamanı</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Son güncelleme zamanı</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Enlem (hesaplanmış property)</summary>
        public double Latitude => Location?.Y ?? 0;

        /// <summary>Boylam (hesaplanmış property)</summary>
        public double Longitude => Location?.X ?? 0;

        /// <summary>Ne kadar süredir tespit edildi</summary>
        public TimeSpan Age => DateTime.UtcNow - DetectedAt;

        /// <summary>Aktif mi? (son 24 saatte tespit edildi + status active)</summary>
        public bool IsActive => Status == FireStatus.Active && Age.TotalHours < 24;

        /// <summary>Risk kategorisi (Low, Medium, High)</summary>
        public string RiskCategory => RiskScore switch
        {
            < 30 => "Low",
            < 70 => "Medium",
            _ => "High"
        };
    }
}
