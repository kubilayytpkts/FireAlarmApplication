namespace FireAlarmApplication.Shared.Contracts.Dtos
{
    /// <summary>
    /// Service health durumu için standart format
    /// Load balancer ve monitoring tools tarafından kullanılır
    /// </summary>
    public class HealthCheckDto
    {
        /// <summary>Service adı (fire-service, alert-service vs.)</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Durum: Healthy, Degraded, Unhealthy</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Service version</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Health check zamanı</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Ek bilgiler (database status, memory usage vs.)</summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new();

        /// <summary>Son başlatılma zamanı</summary>
        public DateTime StartTime { get; set; }

        /// <summary>Kaç süredir çalışıyor</summary>
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;
    }
}
