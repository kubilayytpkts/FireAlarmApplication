using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    /// <summary>
    /// Kullanıcılara özel alert oluşturma ve yönetme servisi
    /// </summary>
    public class UserAlertService : IUserAlertService
    {
        private readonly AlertSystemDbContext _alertSystemDbContext;
        private readonly IGeofencingService _geofencingService;
        private readonly IAlertRuleService _alertRuleService;
        private readonly IRedisService _redisService;
        private readonly ILogger<UserAlertService> _logger;

        public UserAlertService(AlertSystemDbContext alertSystemDbContext, IGeofencingService geofencingService, IAlertRuleService alertRuleService, IRedisService redisService, ILogger<UserAlertService> logger)
        {
            _alertSystemDbContext = alertSystemDbContext;
            _geofencingService = geofencingService;
            _alertRuleService = alertRuleService;
            _redisService = redisService;
            _logger = logger;
        }

        /// <summary>
        /// FireAlert için uygun kullanıcıları bul ve UserAlert oluştur
        /// </summary>
        public async Task<List<UserAlert>> CreateUserAlertsAsync(Guid fireAlertId)
        {
            try
            {
                var fireAlert = await _alertSystemDbContext.FireAlerts.FirstOrDefaultAsync(x => x.Id == fireAlertId);
                if (fireAlert == null)
                {
                    _logger.LogError("FireAlert not found: {AlertId}", fireAlertId);
                    return new List<UserAlert>();
                }

                var nearbyUsers = await _geofencingService.FindUserInRadiusAsync(fireAlert.CenterLatitude, fireAlert.CenterLongitude, 100);
                if (!nearbyUsers.Any())
                {
                    _logger.LogWarning("No users found near fire location");
                    return new List<UserAlert>();
                }

                var userAlerts = new List<UserAlert>();
                var processedUsers = new HashSet<Guid>();

                foreach (var user in nearbyUsers)
                {
                    if (processedUsers.Contains(user.UserId))
                        continue;

                    var distance = _geofencingService.CalculateDistanceKm(user.Latitude, user.Longitude, fireAlert.CenterLatitude, fireAlert.CenterLongitude);

                    var rule = await _alertRuleService.FindApplicableRuleAsync(user.UserRole, distance, fireAlert.OriginalConfidence);

                    var shouldReceive = await ShouldReceiveAlertAsync
                    (
                        user.UserId,
                        fireAlert.CenterLatitude,
                        fireAlert.CenterLongitude,
                        fireAlert.OriginalConfidence
                    );

                    if (!shouldReceive)
                    {
                        _logger.LogDebug("User {UserId} should not receive this alert", user.UserId);
                        continue;
                    }

                    //User Alert oluştur
                    var userAlert = await CreateUserAlertAsync(fireAlert, user, distance, rule);

                    if (userAlert != null)
                    {
                        userAlerts.Add(userAlert);
                        processedUsers.Add(userAlert.UserId);
                    }
                }

                if (userAlerts.Any())
                {
                    _alertSystemDbContext.UserAlerts.AddRange(userAlerts);
                    await _alertSystemDbContext.SaveChangesAsync();

                    _logger.LogInformation("Created {Count} user alerts for FireAlert {AlertId}", userAlerts.Count, fireAlertId);
                }
                return userAlerts;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user alerts for FireAlert {AlertId}", fireAlertId);
                return new List<UserAlert>();
            }
        }

        public async Task<string> GenerateUserSpesificMessageAsync(Guid userId, UserRole userRole, double distance, double confidence)
        {
            try
            {
                //kullanıcı rolünü bul
                var rule = await _alertRuleService.GetRuleForUserRoleAsync(userRole);

                if (rule == null)
                {
                    return $"{distance:F1}km uzağınızda yangın tespit edildi. Güvenirlik: %{confidence:F0}";
                }

                var placeHolders = new Dictionary<string, object>
                {
                    ["Distance"] = distance.ToString("F1"),
                    ["Confidence"] = confidence.ToString("F0"),
                    ["Time"] = DateTime.Now.ToString("HH:mm"),
                    ["Date"] = DateTime.Now.ToString("dd.MM.yyyy")
                };

                var message = _alertRuleService.GenerateMessageFromTemplate(rule.MessageTemplate, placeHolders);

                //message = userRole switch
                //{
                //    UserRole.ForestOfficer => message + " | Acil değerlendirme gerekli.",
                //    UserRole.FireDepartment => message + " | Müdahale ekibi hazır olsun.",
                //    UserRole.LocalGov => message + " | Koordinasyon sağlayın.",
                //    _ => message
                //};

                return message;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user specific message");
                return $"Yangın uyarısı: {distance:F1}km uzağınızda. Güvenirlik: %{confidence:F0}";
            }
        }

        public Task<List<UserAlert>> GetUserAlertsAsync(Guid userId, bool onlyUnread = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> MarkAsReadAsync(Guid fireAlertId, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ShouldReceiveAlertAsync(Guid userId, double fireLatitude, double fireLongitude, double confidence)
        {
            throw new NotImplementedException();
        }
        #region HELPER METHODS
        private async Task<UserAlert?> CreateUserAlertAsync(FireAlert fireAlert, UserLocationInfo user, Double distance, AlertRule alertRule)
        {
            try
            {
                //Aynı kullanıcı için aynı alert kontrolü
                var existingAlert = await _alertSystemDbContext.UserAlerts.AnyAsync(x => x.UserId == user.UserId && x.FireAlert.Id == fireAlert.Id);

                if (existingAlert)
                {
                    _logger.LogDebug("User alert already exists for User {UserId} and FireAlert {AlertId}", user.UserId, fireAlert.Id);
                    return null;
                }

                var personolizedMessage = await GenerateUserSpesificMessageAsync(user.UserId, user.UserRole, distance, fireAlert.OriginalConfidence);

                var userAlert = new UserAlert
                {
                    Id = user.UserId,
                    FireAlertId = fireAlert.Id,
                    UserId = user.UserId,
                    UserRole = user.UserRole,
                    UserLatitude = user.Latitude,
                    UserLongitude = user.Longitude,
                    DistanceToFireKm = Math.Round(distance, 2),
                    AlertMessage = personolizedMessage,
                    CanProvideFeedBack = alertRule.AllowFeedback,
                    IsDelivered = false,
                    CreatedAt = DateTime.UtcNow,
                    FireAlert = fireAlert,
                };

                _logger.LogDebug("UserAlert created for User {UserId} (Role: {Role}, Distance: {Distance}km)", user.UserId, user.UserRole, distance);

                return userAlert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating UserAlert for User {UserId}", user.UserId);
                return null;
            }
        }
        #endregion
    }
}
