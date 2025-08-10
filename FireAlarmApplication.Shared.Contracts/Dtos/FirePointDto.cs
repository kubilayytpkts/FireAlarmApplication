namespace FireAlarmApplication.Shared.Contracts.Dtos
{
    /// <summary>
    /// Yangın noktası bilgileri - servisler arası data transfer için
    /// NASA FIRMS API'sinden gelen data'nın standardize edilmiş hali
    /// </summary>
    public class FirePointDto
    {
        /// <summary>Unique identifier</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Enlem (Latitude) - Türkiye: 36-42 arası</summary>
        public double Latitude { get; set; }

        /// <summary>Boylam (Longitude) - Türkiye: 26-45 arası</summary>
        public double Longitude { get; set; }

        /// <summary>Uydu tarafından tespit edilme zamanı (UTC)</summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>Güven skoru (0-100) - NASA FIRMS confidence değeri</summary>
        public double Confidence { get; set; }

        /// <summary>Parlaklık değeri - T4 kanal brightness</summary>
        public double? Brightness { get; set; }

        /// <summary>Yangın radyatif gücü (MW) - FRP değeri</summary>
        public double? FireRadiativePower { get; set; }

        /// <summary>Hangi uydudan geldi (MODIS, VIIRS)</summary>
        public string Satellite { get; set; } = string.Empty;

        /// <summary>Yangın durumu</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>AI tarafından hesaplanan risk skoru (0-100)</summary>
        public double RiskScore { get; set; } = 0;

        /// <summary>Son güncelleme zamanı</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
