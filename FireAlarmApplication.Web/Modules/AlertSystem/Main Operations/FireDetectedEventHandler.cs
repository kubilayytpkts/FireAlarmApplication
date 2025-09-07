using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Events;
using MediatR;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations
{
    public class FireDetectedEventHandler : INotificationHandler<FireDetectedEvent>
    {
        private readonly IAlertService _alertService;
        private readonly IUserAlertService _userAlertService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<FireDetectedEventHandler> _logger;

        public FireDetectedEventHandler(IAlertService alertService, IUserAlertService userAlertService, INotificationService notificationService, ILogger<FireDetectedEventHandler> logger)
        {
            _alertService = alertService;
            _userAlertService = userAlertService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Handle(FireDetectedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing FireDetectedEvent for fire {FireId} with confidence {Confidence}%", notification.FireId, notification.Confidence);

                //fire alert oluştur
                var fireAlert = await _alertService.CreateFireAlertAsync(notification.FireId, notification.Confidence, notification.Latitude, notification.Longitude);

                //uygun kullanıcıları bul ve user alertler oluştur
                var userAlerts = await _userAlertService.CreateUserAlertsAsync(fireAlert.Id);

                //bildirimleri gönder
                if (userAlerts.Any())
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var results = await _notificationService.SendBatchNotificationAsync(userAlerts);
                            var successCount = results.Count(x => x.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error sending batch notifications for fire {FireId}", notification.FireId);
                        }
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing FireDetectedEvent for fire {FireId}", notification.FireId);
                throw;
            }
        }
    }
}
