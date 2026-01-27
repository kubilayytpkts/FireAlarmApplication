using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class RegionDetectionService : IRegionDetectionService
    {

        private readonly ILogger<RegionDetectionService> _logger;

        public RegionDetectionService(ILogger<RegionDetectionService> logger)
        {
            _logger = logger;
        }

        public SatelliteSourceInfo GetFastestSource(double latitude, double longitude)
        {
            try
            {
                if (IsInMTGCoverage(latitude, longitude))
                {
                    return new SatelliteSourceInfo
                    {
                        Source = SatelliteSource.MTG,
                        Name = "Meteosat Third Generation (MTG)",
                        Description = "Geostationary satellite - Fastest updates every 10 minutes",
                        LatencyMinutes = 15,  // 10-20 dakika ortalama
                        Region = GetMTGRegionName(latitude, longitude)
                    };
                }

                if (IsInVIIRSRealtimeCoverage(latitude, longitude))
                {
                    return new SatelliteSourceInfo
                    {
                        Source = SatelliteSource.VIIRS_Realtime,
                        Name = "VIIRS (NASA - Real-time Stream)",
                        Description = "Near real-time data for North America & Australia",
                        LatencyMinutes = 180,  // 2-4 saat ortalama
                        Region = GetVIIRSRealtimeRegionName(latitude, longitude)
                    };
                }

                return new SatelliteSourceInfo
                {
                    Source = SatelliteSource.VIIRS_Standard,
                    Name = "VIIRS (NASA - Standard)",
                    Description = "Global coverage with 3-6 hour latency",
                    LatencyMinutes = 270,  // 3-6 saat ortalama
                    Region = "Global"
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool IsInMTGCoverage(double latitude, double longitude)
        {
            // MTG-I1 uydusu 0° longitude'de geostationary
            // Kapsama: -80° to +80° longitude, -80° to +80° latitude

            // ÖNEMLİ: MTG'nin görüş açısı sınırlı!
            // Optimal kapsama: -60° to +60° lon, -60° to +60° lat

            // Avrupa (en iyi kapsama)
            if (latitude >= 35 && latitude <= 72 &&
                longitude >= -15 && longitude <= 45)
                return true;

            // Afrika (tam kapsama)
            if (latitude >= -35 && latitude <= 40 &&
                longitude >= -20 && longitude <= 55)
                return true;

            // Orta Doğu (iyi kapsama)
            if (latitude >= 12 && latitude <= 45 &&
                longitude >= 25 && longitude <= 65)
                return true;

            // Güney Afrika
            if (latitude >= -35 && latitude <= -10 &&
                longitude >= 10 && longitude <= 40)
                return true;

            return false;
        }

        public bool IsInVIIRSRealtimeCoverage(double latitude, double longitude)
        {
            // NASA FIRMS VIIRS Near Real-time - Global kapsama alanları

            // Kuzey Amerika (ABD + Kanada + Meksika)
            if (latitude >= 15 && latitude <= 72 &&
                longitude >= -170 && longitude <= -50)
                return true;

            // Güney Amerika
            if (latitude >= -56 && latitude <= 15 &&
                longitude >= -82 && longitude <= -34)
                return true;

            // Avustralya + Yeni Zelanda
            if (latitude >= -50 && latitude <= -10 &&
                longitude >= 110 && longitude <= 180)
                return true;

            // Güneydoğu Asya
            if (latitude >= -12 && latitude <= 30 &&
                longitude >= 90 && longitude <= 145)
                return true;

            // Doğu Asya (Japonya, Kore, Çin doğusu)
            if (latitude >= 20 && latitude <= 55 &&
                longitude >= 100 && longitude <= 150)
                return true;

            // Orta Asya / Sibirya
            if (latitude >= 40 && latitude <= 75 &&
                longitude >= 60 && longitude <= 180)
                return true;

            return false;
        }

        private string GetMTGRegionName(double latitude, double longitude)
        {
            // Türkiye özel kontrolü
            if (latitude >= 36 && latitude <= 42 && longitude >= 26 && longitude <= 45)
                return "Turkey";

            if (latitude >= 35 && latitude <= 72 && longitude >= -15 && longitude <= 45)
                return "Europe";

            if (latitude >= -35 && latitude <= 40 && longitude >= -20 && longitude <= 55)
                return "Africa";

            if (latitude >= 12 && latitude <= 45 && longitude >= 25 && longitude <= 65)
                return "Middle East";

            return "MTG Coverage Area";
        }

        private string GetVIIRSRealtimeRegionName(double latitude, double longitude)
        {
            if (latitude >= 15 && latitude <= 72 && longitude >= -170 && longitude <= -50)
                return "North America";

            if (latitude >= -56 && latitude <= 15 && longitude >= -82 && longitude <= -34)
                return "South America";

            if (latitude >= -50 && latitude <= -10 && longitude >= 110 && longitude <= 180)
                return "Australia/Oceania";

            if (latitude >= -12 && latitude <= 30 && longitude >= 90 && longitude <= 145)
                return "Southeast Asia";

            if (latitude >= 20 && latitude <= 55 && longitude >= 100 && longitude <= 150)
                return "East Asia";

            if (latitude >= 40 && latitude <= 75 && longitude >= 60 && longitude <= 180)
                return "Central/North Asia";

            return "Global";
        }
    }
}
