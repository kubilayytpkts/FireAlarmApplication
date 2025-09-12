namespace FireAlarmApplication.Web.Modules.FireDetection.Models
{
    public class FireDto
    {
        public Guid Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime DetectedAt { get; set; }
        public double Confidence { get; set; }
        public double? Brightness { get; set; }
        public double? FireRadiativePower { get; set; }
        public string Satellite { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double RiskScore { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public TimeSpan Age { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // <summary>
    /// Fire statistics DTO
    /// </summary>
    public class FireStatsDto
    {
        public int TotalFires { get; set; }
        public int ActiveFires { get; set; }
        public int ExtinguishedFires { get; set; }
        public int FalsePositives { get; set; }
        public double AverageRiskScore { get; set; }
        public DateTime LastDetection { get; set; }
        public Dictionary<string, int> FiresByStatus { get; set; } = new();
        public Dictionary<string, int> FiresBySatellite { get; set; } = new();
    }
}
