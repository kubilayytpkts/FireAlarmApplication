namespace FireAlarmApplication.Web.Shared.Common
{
    /// <summary>
    /// Her module bu interface'i implement eder
    /// Dependency injection ve configuration için standart contract
    /// Mikroservis mantığını monolith içinde uygular
    /// </summary>
    public interface IFireGuardModule
    {
        /// <summary>
        /// Module adı - logging ve debugging için
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Module'ın servislerini DI container'a kaydet
        /// DbContext, Services, HttpClients vs.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">App configuration</param>
        void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Module'ın API endpoints'lerini map et
        /// RESTful API'ler için
        /// </summary>
        /// <param name="endpoints">Endpoint route builder</param>
        void ConfigureEndpoints(IEndpointRouteBuilder endpoints);

        /// <summary>
        /// Database seed data ve migration
        /// İlk çalışmada test data oluşturur
        /// </summary>
        /// <param name="serviceProvider">Service provider</param>
        Task SeedDataAsync(IServiceProvider serviceProvider);
    }
}
