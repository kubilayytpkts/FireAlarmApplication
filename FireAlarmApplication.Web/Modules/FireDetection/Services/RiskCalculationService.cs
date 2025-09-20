using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.FireDetection.Data;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class RiskCalculationService : IRiskCalculationService
    {
        private readonly FireDetectionDbContext _fireDetectionDbContext;
        private readonly ILogger<RiskCalculationService> _logger;
        private readonly IOsmGeoDataService _osmGeoDataService;

        private readonly Dictionary<string, double> _riskWeights = new()
        {
            ["confidence_score"] = 0.25,      // Uydu güven seviyesi
            ["fire_intensity"] = 0.20,        // FRP (Fire Radiative Power)  
            ["proximity_to_forest"] = 0.15,   // Ormanlık alana yakınlık
            ["proximity_to_settlement"] = 0.15, // Yerleşim yerine yakınlık
            ["temporal_clustering"] = 0.10,   // Zaman içinde yangın yoğunluğu
            ["spatial_clustering"] = 0.10,    // Aynı bölgede diğer yangınlar
            ["weather_conditions"] = 0.05     // Rüzgar, nem vs.
        };

        public RiskCalculationService(FireDetectionDbContext fireDetectionDbContext, ILogger<RiskCalculationService> logger, IOsmGeoDataService osmGeoDataService)
        {
            _fireDetectionDbContext = fireDetectionDbContext;
            _logger = logger;
            _osmGeoDataService = osmGeoDataService;
        }
        public async Task<double> CalculateRiskScoreAsync(Models.FireDetection fire)
        {
            try
            {
                double totalRisk = 0;

                //Confidence Score (0 - 25 puan)
                var confidenceScore = (fire.Confidence / 100.0) * _riskWeights["confidence_score"] * 100;
                totalRisk += confidenceScore;

                //Fire Insensity FRP (0 - 20 puan) 
                var intensityScore = CalculateIntensityScore(fire.FireRadiativePower ?? 0) * _riskWeights["fire_intensity"];
                totalRisk += intensityScore;

                //OSM Forest Proximinty (0 - 15 puan)
                var forestScore = await CalculateForestProximintyScore(fire.Latitude, fire.Longitude);
                totalRisk += forestScore;

                //0SM Settlement Proximinty (0 - 15 puan)
                var settlementScore = await CalculateSettlementProximityScore(fire.Latitude, fire.Longitude);
                totalRisk += settlementScore;

                // 5. Temporal Clustering (şimdilik basit) Zaman içinde yangın yoğunluğu
                var temporalScore = 50; // TODO: Implement
                totalRisk += temporalScore * _riskWeights["temporal_clustering"];

                // 6. Spatial Clustering (şimdilik basit) Aynı bölgede diğer yangınlar
                var spatialScore = 50; // TODO: Implement
                totalRisk += spatialScore * _riskWeights["spatial_clustering"];

                // 7. Weather Conditions (şimdilik basit)  Rüzgar, nem vs.
                var weatherScore = 50; // TODO: Implement
                totalRisk += weatherScore * _riskWeights["weather_conditions"];

                var finalRiskScore = Math.Clamp(totalRisk, 0, 100);
                return Math.Round(finalRiskScore, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating OSM risk for fire {FireId}", fire.Id);

                return 0;
            }
        }

        public Task<double> AssessLocationRiskAsync(double latitude, double longitude)
        {
            throw new NotImplementedException();
        }


        public Task<Dictionary<Guid, double>> CalculateRiskScoresAsync(List<Models.FireDetection> fires)
        {
            throw new NotImplementedException();
        }

        public Task<RiskAssessmentResult> GetDetailedRiskAsync(Models.FireDetection fire)
        {
            throw new NotImplementedException();
        }

        #region Helper Methods
        private double CalculateIntensityScore(double frp)
        {
            return frp switch
            {
                >= 100 => 100, // Mega fire
                >= 50 => 85,   // Büyük yangın
                >= 20 => 70,   // Orta yangın  
                >= 10 => 50,   // Küçük yangın
                >= 5 => 30,    // Çok küçük
                _ => 15        // Minimal
            };
        }

        private async Task<Double> CalculateForestProximintyScore(double lat, double lng)
        {
            try
            {
                if (await _osmGeoDataService.IsInForestAreaAsync(lat, lng))
                {
                    return 100; // Orman içinde
                }

                // en yakın ormana mesafe 
                var distanceToForest = await _osmGeoDataService.GetDistanceToNearestForestAsync(lat, lng);

                var score = distanceToForest switch
                {
                    <= 1 => 95,    // 1 km içinde - kritik
                    <= 5 => 85,    // 5 km içinde - çok yüksek
                    <= 10 => 70,   // 10 km içinde - yüksek
                    <= 20 => 50,   // 20 km içinde - orta
                    <= 30 => 25,   // 30 km içinde - düşük
                    double.MaxValue => 5, // Orman bulunamadı
                    _ => 10        // 30 km+ - çok düşük          
                };

                return score;

            }
            catch (Exception ex)
            {
                return 50;
            }
        }
        private async Task<double> CalculateSettlementProximityScore(double lat, double lng)
        {
            try
            {
                if (await _osmGeoDataService.IsInSettlementAreaAsync(lat, lng))
                {
                    return 100;// Yerleşim içinde 
                }

                // En yakın yerleşime mesafe
                var distanceToSettlement = await _osmGeoDataService.GetDistanceToNearestSettlementAsync(lat, lng);

                var score = distanceToSettlement switch
                {
                    <= 2 => 95,   // 2 km içinde - kritik (tahliye mesafesi)
                    <= 10 => 80,   // 10 km içinde - yüksek
                    <= 25 => 60,   // 25 km içinde - orta
                    <= 30 => 35,   // 30 km içinde - düşük
                    double.MaxValue => 5, // Yerleşim bulunamadı
                    _ => 5          // 30 km+ - minimal
                };

                return score;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating settlement proximity for ({Lat}, {Lng})", lat, lng);
                return 50; // Hata durumunda orta risk
            }
        }
        #endregion
    }
}