namespace FireAlarmApplication.Shared.Contracts.Enums
/// <summary>
/// Alert seviye türleri - risk skoruna göre belirlenir
/// </summary>
{
    public enum AlertPriority
    {
        /// <summary>Düşük risk (0-20)</summary>
        Low = 0,

        /// <summary>Orta risk (21-40)</summary>
        Medium = 1,

        /// <summary>Yüksek risk (41-70)</summary>
        High = 2,

        /// <summary>Kritik risk (71-90)</summary>
        Critical = 3,

        /// <summary>Acil durum (91-100)</summary>
        Emergency = 4
    }
}
