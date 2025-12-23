namespace FireAlarmApplication.Web.Shared.Common;

/// <summary>
/// FireGuard application configuration
/// appsettings.json'daki FireGuard section'ını map eder
/// </summary>
public class FireGuardOptions
{
    public const string SectionName = "FireGuard";

    public NasaFirmsOptions NasaFirms { get; set; } = new();
    public CacheOptions Cache { get; set; } = new();
    public RabbitMQOptions RabbitMQ { get; set; } = new();
    public ConnectionStrings ConnectionStrings { get; set; } = new();
    public EumetsatOptions Eumetsat { get; set; } = new();
}

/// <summary>
/// NASA FIRMS API configuration
/// </summary>
public class NasaFirmsOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultArea { get; set; } = "36,26,42,45"; // Turkey bounds
    public int DefaultDayRange { get; set; } = 1;
}

public class EumetsatOptions
{
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
}

/// <summary>
/// Cache configuration
/// </summary>
public class CacheOptions
{
    public int DefaultExpirationMinutes { get; set; } = 15;
    public int FireDataExpirationMinutes { get; set; } = 5;
    public int UserSessionExpirationHours { get; set; } = 24;
}

/// <summary>
/// RabbitMQ configuration
/// </summary>
public class RabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; }
}