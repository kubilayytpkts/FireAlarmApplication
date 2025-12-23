using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Web.Modules.FireDetection.Models
{
    public class FireDetectionResponse
    {
        public SatelliteSourceInfo SourceInfo { get; set; }
        public UserLocation UserLocation { get; set; }
        public double RadiusKm { get; set; }
        public int FireCount { get; set; }
        public List<FireDetectionDto> Fires { get; set; }
    }

    public class UserLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class FireDetectionDto
    {
        public Guid Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceKm { get; set; }
        public DateTime DetectedAt { get; set; }
        public string Satellite { get; set; }
        public double Confidence { get; set; }
        public string Status { get; set; }
        public int AgeMinutes { get; set; }
    }
}
