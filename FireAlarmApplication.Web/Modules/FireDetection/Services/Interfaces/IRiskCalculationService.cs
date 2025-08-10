namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    /// <summary>
    /// AI-based risk calculation service
    /// Fire risk scoring algorithms
    /// </summary>
    public interface IRiskCalculationService
    {
        /// <summary>Yangın için risk skoru hesapla</summary>
        Task<double> CalculateRiskScoreAsync(Models.FireDetection fire);

        /// <summary>Multiple fire'lar için batch risk calculation</summary>
        Task<Dictionary<Guid, double>> CalculateRiskScoresAsync(List<Models.FireDetection> fires);

        /// <summary>Location-based risk assessment</summary>
        Task<double> AssessLocationRiskAsync(double latitude, double longitude);
    }
}
