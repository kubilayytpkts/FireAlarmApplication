using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    /// <summary>
    /// RabbitMQ ile notification gönderimi
    /// Fire-and-forget pattern + retry logic + fallback mechanism
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly FireGuardOptions _fireGuardOptions;


        // Exchange and Queue names
        private const string EXCHANGE_NAME = "fireguard.notifications";
        private const string EMAIL_QUEUE = "fireguard.notifications.email";
        private const string SMS_QUEUE = "fireguard.notifications.sms";
        private const string PUSH_QUEUE = "fireguard.notifications.push";
        private const string DEAD_LETTER_QUEUE = "fireguard.notifications.dlq";

        public NotificationService(ILogger<NotificationService> logger, IConfiguration configuration, IOptions<FireGuardOptions> fireGuardOptions)
        {
            try
            {
                _logger = logger;
                _fireGuardOptions = fireGuardOptions.Value;

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

                _connection = factory.CreateConnection("FireGuard-NotificationService");
                _channel = _connection.CreateModel();

                SetupRabbitMQTopology();

            }
            catch (Exception EX)
            {
                throw EX;
            }

        }

        /// <summary>
        /// Bildirim gönderimini işaretle
        /// TODO: Database update işlemi eklenecek
        /// </summary>
        public async Task<bool> MarkAsDeliveridAsync(Guid userAlertId)
        {
            try
            {
                // TODO: Database update
                _logger.LogInformation("✅ UserAlert {Id} marked as delivered", userAlertId);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to mark UserAlert {Id} as delivered", userAlertId);
                return false;
            }
        }

        /// <summary>
        /// Toplu bildirim gönderimi - paralel processing
        /// </summary>
        public async Task<Dictionary<Guid, bool>> SendBatchNotificationAsync(List<UserAlert> userAlerts)
        {
            var results = new Dictionary<Guid, bool>();

            try
            {
                var paralelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10,
                };

                await Parallel.ForEachAsync(userAlerts, paralelOptions, async (userAlert, ct) =>
                {
                    try
                    {
                        var success = await SendNotificationAsync(userAlert);
                        lock (results)
                        {
                            results[userAlert.Id] = success;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification for UserAlert {Id}", userAlert.Id);
                        lock (results)
                        {
                            results[userAlert.Id] = false;
                        }
                    }
                });

                var successCount = results.Count(x => x.Value == true);
                _logger.LogInformation("Batch notification completed: {Success}/{Total} successful", successCount, results.Count);

                return results;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in batch notification");

                // Hata durumunda tüm alertleri false olarak işaretle
                return userAlerts.ToDictionary(ua => ua.Id, ua => false);
            }
        }

        public Task<bool> SendEmailNotificationAsync(UserAlert userAlert)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Kullanıcı tercihine göre uygun kanaldan bildirim gönder
        /// TODO: User preferences'a göre kanal seçimi yapılacak
        /// </summary>
        public async Task<bool> SendNotificationAsync(UserAlert userAlert)
        {
            try
            {
                // Şimdilik tüm kanallardan gönder (sonra user preference eklenecek)
                var results = new List<bool>();

                //kritik alertler için tüm kanallar
                if (userAlert.FireAlert?.Severity >= AlertSeverity.High)
                {
                    results.Add(await SendPushNotificationAsync(userAlert));
                    results.Add(await SendSmsNotificationAsync(userAlert));
                    results.Add(await SendEmailNotificationAsync(userAlert));
                }
                else
                {
                    //normal bildirimler için sadece push
                    results.Add(await SendPushNotificationAsync(userAlert));
                }
                return results.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending notification for UserAlert {UserAlertId}", userAlert.Id);
                return false;
            }
        }

        public Task<bool> SendPushNotificationAsync(UserAlert userAlert)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SendSmsNotificationAsync(UserAlert userAlert)
        {
            throw new NotImplementedException();
        }


        #region HELPER METHODS 
        /// <summary>
        /// RabbitMQ topology setup - exchanges, queues, bindings
        /// </summary>
        private void SetupRabbitMQTopology()
        {
            // Declare exchange
            _channel.ExchangeDeclare(
                exchange: EXCHANGE_NAME,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Dead Letter Exchange
            _channel.ExchangeDeclare(
                exchange: "fireguard.dlx",
                type: ExchangeType.Direct,
                durable: true);

            // Setup queues with priority and DLX
            var queueArgs = new Dictionary<string, object>
            {
                ["x-max-priority"] = 10,
                ["x-message-ttl"] = 86400000, // 24 hours in milliseconds
                ["x-dead-letter-exchange"] = "fireguard.dlx"
            };

            // Email Queue
            _channel.QueueDeclare(
                queue: EMAIL_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            _channel.QueueBind(EMAIL_QUEUE, EXCHANGE_NAME, "notification.email.*");

            // SMS Queue
            _channel.QueueDeclare(
                queue: SMS_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            _channel.QueueBind(SMS_QUEUE, EXCHANGE_NAME, "notification.sms.*");

            // Push Queue
            _channel.QueueDeclare(
                queue: PUSH_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            _channel.QueueBind(PUSH_QUEUE, EXCHANGE_NAME, "notification.push.*");

            // Dead Letter Queue
            _channel.QueueDeclare(
                queue: DEAD_LETTER_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false);

            _channel.QueueBind(DEAD_LETTER_QUEUE, "fireguard.dlx", "");

            // Enable publisher confirms
            _channel.ConfirmSelect();

            _logger.LogInformation("📦 RabbitMQ topology created: Exchange={Exchange}, Queues=[email, sms, push, dlq]",
                EXCHANGE_NAME);
        }

        /// <summary>
        /// RabbitMQ'ya mesaj publish et
        /// </summary>
        private async Task<bool> PublishNotificationAsync(UserAlert userAlert, string notificationType, string routingKey)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var message = new NotificationMessage
                    {
                        UserAlertId = userAlert.Id,
                        UserId = userAlert.UserId,
                        NotificationType = notificationType,
                        Priority = userAlert.FireAlert?.Severity ?? AlertSeverity.Low,
                        Title = userAlert.FireAlert.Title ?? "Yangın Uyarısı",
                        Message = userAlert.AlertMessage,
                        Metadata = new Dictionary<string, object>
                        {
                            ["FireAlertId"] = userAlert.FireAlertId,
                            ["UserRole"] = userAlert.UserRole.ToString(),
                            ["DistanceKm"] = userAlert.DistanceToFireKm,
                            ["Latitude"] = userAlert.UserLatitude,
                            ["Longitude"] = userAlert.UserLongitude
                        },
                        RetryCount = 0,
                        CreatedAt = DateTime.UtcNow,
                    };

                    var json = JsonSerializer.Serialize(message);
                    var body = Encoding.UTF8.GetBytes(json);

                    // Message properties
                    var properties = _channel.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.Priority = GetPriorityValue(message.Priority);
                    properties.MessageId = Guid.NewGuid().ToString();
                    properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    properties.Headers = new Dictionary<string, object>
                    {
                        ["notification-type"] = notificationType,
                        ["user-role"] = userAlert.UserRole.ToString()
                    };

                    _channel.BasicPublish(
                        exchange: EXCHANGE_NAME,
                        routingKey: routingKey,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);

                    var confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));

                    if (confirmed)
                    {
                        _logger.LogDebug("{Type} notification published for UserAlert {Id}", notificationType, userAlert.Id);
                        return true;
                    }

                    _logger.LogWarning("{Type} notification not confirmed for UserAlert {Id}", notificationType, userAlert.Id);
                    return false;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish {Type} notification for UserAlert {Id}", notificationType, userAlert.Id);
                return false;
            }
        }

        /// <summary>
        /// Convert AlertSeverity to RabbitMQ priority value
        /// </summary>
        private byte GetPriorityValue(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical => 10,
                AlertSeverity.High => 7,
                AlertSeverity.Medium => 5,
                AlertSeverity.Low => 3,
                AlertSeverity.Info => 1,
                _ => 1
            };
        }
        #endregion
    }
}
