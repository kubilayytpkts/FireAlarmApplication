using FireAlarmApplication.Shared.Contracts.Models;

public interface IOsmGeoDataService
{
    Task<bool> IsInForestAreaAsync(double lat, double lng);        // Orman kontrolü
    Task<bool> IsInSettlementAreaAsync(double lat, double lng);    // Yerleşim kontrolü  
    Task<bool> IsInProtectedAreaAsync(double lat, double lng);     // Korunan alan kontrolü
    public Task<bool> IsUserInTurkey(double latitude, double longitude); // Türkiye sınırları içerisinde mi kontrolü

    Task<double> GetDistanceToNearestForestAsync(double lat, double lng);     // Orman mesafesi
    Task<double> GetDistanceToNearestSettlementAsync(double lat, double lng); // Yerleşim mesafesi

    Task<OSMAreaInfo> GetAreaInfoAsync(double lat, double lng);    // Detaylı bilgi
    Task RefreshAreaDataAsync(string bbox);                       // Cache yenileme
    Task<bool> IsServiceHealthyAsync();                           // Sistem sağlığı
}

