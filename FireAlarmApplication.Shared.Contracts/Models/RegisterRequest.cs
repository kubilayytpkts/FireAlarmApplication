namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public LocationInfo? InitialLocation { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string? FcmToken { get; set; }
        public string? ApnsToken { get; set; }
    }
    public class UpdateProfileRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool EnableEmail { get; set; } = true;
        public bool EnableSms { get; set; } = false;
        public bool EnablePush { get; set; } = true;
    }

    public class LocationUpdateRequest
    {
        public double Latitude { get; set; }
        public double longitude { get; set; }
        public double Accuracy { get; set; } = 10; // 10 metre hata payı
        public double Speed { get; set; } = 0; //ms
        public double Heading { get; set; } // 0 - 360 degress
        public DateTime? TimeStamp { get; set; }
    }

    public class TrackingRequest
    {
        public bool Enable { get; set; }
    }

    public class LocationInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
