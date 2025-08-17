namespace FireAlarmApplication.Shared.Contracts.Enums
{
    public enum AlertSeverity
    {
        Info = 0,           // Confidence 30-40: "Şüpheli tespit" 
        Low = 1,            // Confidence 40-55: "Düşük risk"
        Medium = 2,         // Confidence 55-70: "Orta risk"
        High = 3,           // Confidence 70-85: "Yüksek risk"
        Critical = 4        // Confidence 85+: "Kritik durum"
    }
    public enum AlertStatus
    {
        Active = 0,         // Aktif uyarı
        Confirmed = 1,      // Vatandaş teyidi var
        Denied = 2,         // Vatandaş reddi var  
        Resolved = 3,       // Yangın söndürüldü
        Expired = 4         // Zaman aşımı (24 saat sonra)
    }
    public enum UserRole
    {
        Civilian = 0,           // Vatandaş - sadece kesin yangınlar
        ForestOfficer = 1,      // Orman görevlisi - düşük confidence'lı bile
        FireDepartment = 2,     // İtfaiye - tüm yangınlar  
        LocalGov = 3,           // Belediye/AFAD - bölgesel
        SystemAdmin = 4         // Sistem yöneticisi - her şey
    }
    public enum FeedbackType
    {
        Confirmed = 0,      // "Evet, yangın var"
        Denied = 1,         // "Hayır, yangın yok"
        Uncertain = 2       // "Emin değilim"
    }
}
