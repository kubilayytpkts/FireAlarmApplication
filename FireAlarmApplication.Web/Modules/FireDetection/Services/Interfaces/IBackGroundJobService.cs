namespace FireAlarmApplication.Web.Modules.FireDetection.Services.Interfaces
{
    public interface IBackGroundJobService
    {
        /// <summary>Periyodik NASA sync job'unu başlat</summary>
        void ScheduleNasaSync();

        /// <summary>Manuel NASA sync trigger</summary>
        Task<string> TriggerManualSyncAsync();
    }
}
