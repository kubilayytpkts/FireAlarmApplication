namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class LocationResponse
    {
        public bool HasLocation { get; set; }
        public double? CurrentLatitude { get; set; }
        public double? CurrentLongitude { get; set; }
        public double? HomeLatitude { get; set; }
        public double? HomeLongitude { get; set; }
        public double LocationAccuracy { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
        public bool TrackingEnabled { get; set; }
    }
}
