using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Consumers
{
    public class PushNotificationConsumer : IHostedService, IDisposable
    {
        private readonly ILogger<PushNotificationConsumer> _logger;
        private readonly HttpClient _httpClient;
        private readonly FireGuardOptions _fireGuardOptions;
        private IConnection _connection;
        private IModel _channel;
        private const string PUSH_QUEUE = "fireguard.notifications.push";

        public PushNotificationConsumer(
            ILogger<PushNotificationConsumer> logger,
            HttpClient httpClient,
            IOptions<FireGuardOptions> fireGuardOptions)
        {
            _logger = logger;
            _httpClient = httpClient;
            _fireGuardOptions = fireGuardOptions.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _fireGuardOptions.RabbitMQ.HostName,
                    Port = _fireGuardOptions.RabbitMQ.Port,
                    UserName = _fireGuardOptions.RabbitMQ.UserName,
                    Password = _fireGuardOptions.RabbitMQ.Password,
                    VirtualHost = _fireGuardOptions.RabbitMQ.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat = TimeSpan.FromSeconds(60),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                };

                _connection = factory.CreateConnection("FireGuard-PushConsumer");
                _channel = _connection.CreateModel();
                _channel.BasicQos(0, 10, global: false);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (sender, ea) =>
                {
                    await HandleMessage(ea);
                };

                _channel.BasicConsume(
                    queue: PUSH_QUEUE,
                    autoAck: false,
                    consumer: consumer
                );

                _logger.LogInformation("PushNotificationConsumer started, listening on {Queue}", PUSH_QUEUE);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PushNotificationConsumer");
                throw;
            }
        }

        private async Task HandleMessage(BasicDeliverEventArgs ea)
        {
            var message = JsonSerializer.Deserialize<NotificationMessage>(
                Encoding.UTF8.GetString(ea.Body.Span));

            if (message == null)
            {
                _channel.BasicReject(ea.DeliveryTag, requeue: false);
                _logger.LogError("Invalid message received, rejected");
                return;
            }

            try
            {
                var success = await SendToExpoAsync(message);

                if (success)
                {
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("Push sent successfully for UserAlert {Id}", message.UserAlertId);
                }
                else
                {
                    HandleFailedMessage(ea, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing push notification for UserAlert {Id}", message.UserAlertId);
                HandleFailedMessage(ea, message);
            }
        }

        private async Task<bool> SendToExpoAsync(NotificationMessage message)
        {
            var expoToken = message.Metadata.ContainsKey("FcmToken")
                ? message.Metadata["FcmToken"]?.ToString()
                : message.Metadata.ContainsKey("ApnsToken")
                    ? message.Metadata["ApnsToken"]?.ToString()
                    : null;

            if (string.IsNullOrEmpty(expoToken))
            {
                _logger.LogWarning("No ExpoToken in metadata for UserAlert {Id}", message.UserAlertId);
                return false;
            }

            var payload = new
            {
                to = expoToken,
                sound = "default",
                title = message.Title,
                body = message.Message,
                priority = message.Priority >= AlertSeverity.Critical ? "high" : "normal",
                channelId = "fire_alerts",
                data = new
                {
                    type = "fire_alert",
                    fireAlertId = message.Metadata.GetValueOrDefault("FireAlertId"),
                    latitude = message.Metadata.GetValueOrDefault("Latitude"),
                    longitude = message.Metadata.GetValueOrDefault("Longitude"),
                    severity = message.Priority.ToString(),
                    distanceKm = message.Metadata.GetValueOrDefault("DistanceKm")
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "https://exp.host/--/api/v2/push/send",
                payload
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                _logger.LogError("Expo Push API failed: {Status} - {Response}", response.StatusCode, errorText);
                return false;
            }

            return true;
        }

        private void HandleFailedMessage(BasicDeliverEventArgs ea, NotificationMessage message)
        {
            message.RetryCount++;

            if (message.RetryCount >= 3)
            {
                _channel.BasicReject(ea.DeliveryTag, requeue: false); // DLX'a gider
                _logger.LogWarning("Push notification failed after {Retries} retries for UserAlert {Id}, sent to DLX",
                    message.RetryCount, message.UserAlertId);
            }
            else
            {
                _channel.BasicReject(ea.DeliveryTag, requeue: true); // Tekrar queue'ya
                _logger.LogDebug("Push notification requeued (attempt {Retry}) for UserAlert {Id}",
                    message.RetryCount, message.UserAlertId);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PushNotificationConsumer stopped");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}