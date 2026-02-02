using FireAlarmApplication.Shared.Contracts.Enums;

public class UserLocationInfo
{
    public Guid UserId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public UserRole UserRole { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? FcmToken { get; set; }
    public string? ApnsToken { get; set; }
    public bool IsActive { get; set; }

}