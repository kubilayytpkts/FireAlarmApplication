namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    public interface IBackGroundJobService
    {
        /// <summary>Tüm recurring job'ları schedule et (startup'ta çağrılır)</summary>
        void ScheduleAllJobs();

        /// <summary>Periyodik uydu veri sync job'unu başlat</summary>
        void ScheduleFireDataSync();

        /// <summary>Expired alert'leri temizleme job'ı</summary>
        void ScheduleExpiredAlertCleanup();

        /// <summary>Expired/eski fire detection kayıtlarını temizleme job'ı</summary>
        void ScheduleExpiredFireCleanup();

        /// <summary>Manuel sync trigger (test/admin amaçlı)</summary>
        Task<string> TriggerManualSyncAsync();
    }
}
