namespace FireAlarmApplication.Shared.Contracts.Enums
/// <summary>
/// Alert türleri - ne tür uyarı gönderileceği
/// </summary>
{
    public enum AlertType
    {
        /// <summary>Yeni yangın tespit edildi</summary>
        FireDetected = 0,

        /// <summary>Yangın yaklaşıyor</summary>
        FireApproaching = 1,

        /// <summary>Tahliye uyarısı</summary>
        EvacuationWarning = 2,

        /// <summary>Hava durumu uyarısı</summary>
        WeatherAlert = 3,

        /// <summary>Tehlike geçti</summary>
        AllClear = 4
    }
}
