namespace FireAlarmApplication.Shared.Contracts.Enums
{
    public enum SatelliteSource
    {
        MTG,              // En hızlı: 10-20 dakika
        VIIRS_Realtime,   // Hızlı: 2-4 saat (sadece belirli bölgeler)
        VIIRS_Standard,   // Orta: 3-6 saat
        MODIS             // Orta: 3-6 saat
    }

    public class SatelliteSourceInfo
    {
        public SatelliteSource Source { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int LatencyMinutes { get; set; }  // Ortalama gecikme
        public string Region { get; set; }
    }
}
