namespace FireAlarmApplication.Shared.Contracts.Models
{
    public class BaseResponse<T>
    {
        /// <summary>İşlem başarılı mı?</summary>
        public bool Success { get; set; }

        /// <summary>İşlem mesajı</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Dönen data (generic type)</summary>
        public T? Data { get; set; }

        /// <summary>Hata mesajları listesi</summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>Response oluşturulma zamanı</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Başarılı response oluşturmak için helper method
        /// </summary>
        public static BaseResponse<T> SuccessResult(T data, string message = "Success")
        {
            return new BaseResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Hata response oluşturmak için helper method
        /// </summary>
        public static BaseResponse<T> ErrorResult(string message, List<string>? errors = null)
        {
            return new BaseResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>(),
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
