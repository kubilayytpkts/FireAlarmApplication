using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces;
using Hangfire;

namespace FireAlarmApplication.Web.Modules.FireDetection.Services
{
    public class BackGroundJobService : IBackGroundJobService
    {
        private readonly ILogger<BackGroundJobService> _logger;
        private readonly IRecurringJobManager _recurringJobManager;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public BackGroundJobService(ILogger<BackGroundJobService> logger, IRecurringJobManager recurringJobManager, IBackgroundJobClient backgroundJobClient)
        {
            _logger = logger;
            _recurringJobManager = recurringJobManager;
            _backgroundJobClient = backgroundJobClient;
        }

        /// <summary>
        /// Tum recurring job'lari schedule et
        /// Program.cs startup'inda cagrilir
        /// </summary>
        public void ScheduleAllJobs()
        {
            try
            {
                ScheduleFireDataSync();
                ScheduleExpiredAlertCleanup();
                ScheduleExpiredFireCleanup();

                _logger.LogInformation("All recurring jobs scheduled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling recurring jobs");
            }
        }

        /// <summary>
        /// Uydu veri senkronizasyonu - her 15 dakikada bir
        /// MTG (Avrupa/Turkiye) + NASA FIRMS (Global) verilerini ceker ve DB'ye yazar
        /// </summary>
        public void ScheduleFireDataSync()
        {
            try
            {
                _recurringJobManager.AddOrUpdate<IFireDataSyncService>(
                    recurringJobId: "global-fire-data-sync",
                    methodCall: service => service.SyncFiresFromSatellitesAsync(),
                    cronExpression: "*/15 * * * *",
                    options: new RecurringJobOptions
                    {
                        TimeZone = TimeZoneInfo.Utc,
                        MisfireHandling = MisfireHandlingMode.Ignorable

                    }
                );

                _logger.LogInformation("Fire data sync job scheduled (every 15 minutes)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling fire data sync job");
            }
        }

        /// <summary>
        /// Expired alert temizligi - her saat basi
        /// 24 saati gecmis alert'leri Expired durumuna ceker
        /// </summary>
        public void ScheduleExpiredAlertCleanup()
        {
            try
            {
                _recurringJobManager.AddOrUpdate<IAlertService>(
                    recurringJobId: "expired-alert-cleanup",
                    methodCall: service => service.CleanupExpiredAlertsAsync(),
                    cronExpression: "0 * * * *",
                    options: new RecurringJobOptions
                    {
                        TimeZone = TimeZoneInfo.Utc
                    }
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling expired alert cleanup job");
            }
        }


        /// <summary>
        /// Eski fire detection kayitlarini temizle - gunde bir kez
        /// 7 gunden eski Extinguished/FalsePositive kayitlari siler
        /// </summary>
        public void ScheduleExpiredFireCleanup()
        {
            try
            {
                _recurringJobManager.AddOrUpdate<IFireDataSyncService>(
                    recurringJobId: "expired-fire-cleanup",
                    methodCall: service => service.CleanupOldFireDetectionsAsync(),
                    cronExpression: "0 3 * * *",
                    options: new RecurringJobOptions
                    {
                        TimeZone = TimeZoneInfo.Utc
                    }
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling expired fire cleanup job");
            }
        }



        /// <summary>
        /// Periyodik NASA sync job schedule et
        /// Her 15 dakikada bir çalışsın
        /// </summary>
        public void ScheduleNasaSync()
        {
            try
            {
                // Recurring job - her 15 dakikada bir
                //_recurringJobManager.AddOrUpdate<IFireDataSyncService>(
                //    "nasa-sync-job",
                //    service => service.SyncFiresFromNasaAsync(),
                //    "*/15****"
                //    );

                _logger.LogInformation("NASA sync job scheduled (every 15 minutes)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling NASA sync job");
            }
        }
        /// <summary>
        /// Manuel sync trigger (test amaçlı)
        /// </summary>
        public async Task<string> TriggerManualSyncAsync()
        {
            try
            {
                var jobId = _backgroundJobClient.Enqueue<IFireDataSyncService>(
                    service => service.SyncFiresFromSatellitesAsync()
                );

                _logger.LogInformation("Manual NASA sync job triggered immediately: {JobId}", jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering manual sync");
                return "Error: " + ex.Message;
            }
        }
    }
}
