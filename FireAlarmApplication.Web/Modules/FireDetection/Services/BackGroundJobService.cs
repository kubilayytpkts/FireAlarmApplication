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
        /// Periyodik NASA sync job schedule et
        /// Her 15 dakikada bir çalışsın
        /// </summary>
        public void ScheduleNasaSync()
        {
            try
            {
                // Recurring job - her 15 dakikada bir
                _recurringJobManager.AddOrUpdate<IFireDataSyncService>(
                    "nasa-sync-job",
                    service => service.SyncFiresFromNasaAsync(),
                    "*/15****"
                    );

                _logger.LogInformation("🕐 NASA sync job scheduled (every 15 minutes)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error scheduling NASA sync job");
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
               service => service.SyncFiresFromNasaAsync());

                _logger.LogInformation("🚀 Manual NASA sync job triggered: {JobId}", jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error triggering manual sync");
                return "Error: " + ex.Message;
            }
        }
    }
}
