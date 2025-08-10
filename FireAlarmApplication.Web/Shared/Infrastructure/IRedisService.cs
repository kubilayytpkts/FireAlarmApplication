namespace FireAlarmApplication.Web.Shared.Infrastructure
{
    public interface IRedisService
    {/// <summary>
     /// Cache'den veri al
     /// </summary>
     /// <typeparam name="T">Veri tipi</typeparam>
     /// <param name="key">Cache key</param>
     /// <returns>Cache'deki veri veya null</returns>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Cache'e veri kaydet
        /// </summary>
        /// <typeparam name="T">Veri tipi</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Kaydedilecek veri</param>
        /// <param name="expiry">Expire süresi (null = never expire)</param>
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);

        /// <summary>
        /// Cache'den veri sil
        /// </summary>
        /// <param name="key">Cache key</param>
        Task RemoveAsync(string key);

        /// <summary>
        /// Cache'de key var mı kontrol et
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>True = var, False = yok</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Key'in expire time'ını set et
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="expiry">Yeni expire süresi</param>
        Task SetExpireAsync(string key, TimeSpan expiry);

        /// <summary>
        /// Multiple key'leri al
        /// </summary>
        /// <typeparam name="T">Veri tipi</typeparam>
        /// <param name="keys">Key listesi</param>
        /// <returns>Key-Value dictionary</returns>
        Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys);
    }
}
