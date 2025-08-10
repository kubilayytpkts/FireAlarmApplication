namespace FireAlarmApplication.Web.Shared.Common
{
    public static class ModuleExtensions
    {
        /// <summary>
        /// Tüm module'ları register eder
        /// </summary>
        public static WebApplicationBuilder AddFireGuardModules(this WebApplicationBuilder builder, params IFireGuardModule[] modules)
        {
            // Her module'ı service'lere kaydet
            foreach (var module in modules)
            {
                module.ConfigureServices(builder.Services, builder.Configuration);
            }

            // Module listesini DI'da sakla (endpoint mapping için)
            builder.Services.AddSingleton<IReadOnlyList<IFireGuardModule>>(modules.ToList());

            return builder;
        }

        /// <summary>
        /// Tüm module endpoints'lerini map eder
        /// </summary>
        public static WebApplication MapFireGuardModules(this WebApplication app)
        {
            var modules = app.Services.GetRequiredService<IReadOnlyList<IFireGuardModule>>();

            foreach (var module in modules)
            {
                module.ConfigureEndpoints(app);
            }

            return app;
        }


        /// <summary>
        /// Tüm module'ların seed data'sını çalıştırır
        /// </summary>
        public static async Task SeedFireGuardModulesAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var modules = scope.ServiceProvider.GetRequiredService<IReadOnlyList<IFireGuardModule>>();

            foreach (var module in modules)
            {
                await module.SeedDataAsync(scope.ServiceProvider);
            }
        }
    }
}
