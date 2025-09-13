using System.Net;

namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class ServiceResponse<T>
    {
        public bool Success { get; set; }         // İşlem başarılı mı
        public string? Message { get; set; }      // Hata veya bilgi mesajı
        public T? Data { get; set; }              // Opsiyonel veri
        public HttpStatusCode StatusCode { get; set; }  // 200, 400, 404 vs.
    }
}
