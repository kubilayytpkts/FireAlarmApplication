namespace FireAlarmApplication.Shared.Contracts.Enums
{
    public enum FireStatus
    {
        /// <summary>Yeni tespit edildi, henüz doğrulanmadı</summary>
        Detected = 0,

        /// <summary>Doğrulandı, gerçek yangın</summary>
        Verified = 1,

        /// <summary>Aktif yangın devam ediyor</summary>
        Active = 2,

        /// <summary>Kontrol altına alındı ama devam ediyor</summary>
        Contained = 3,

        /// <summary>Tamamen söndürüldü</summary>
        Extinguished = 4,

        /// <summary>Yanlış alarm, gerçek yangın değil</summary>
        FalsePositive = 5
    }
}
