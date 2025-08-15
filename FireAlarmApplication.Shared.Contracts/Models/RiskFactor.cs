using FireAlarmApplication.Shared.Contracts.Enums;

namespace FireAlarmApplication.Shared.Contracts.Models
{

    public class RiskAssessmentResult
    {
        public double TotalRiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public List<RiskFactor> Factors { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RiskFactor
    {
        public string Name { get; set; } = string.Empty;
        public double Score { get; set; }
        public double Weight { get; set; }
        public string Description { get; set; } = string.Empty;
    }


}
