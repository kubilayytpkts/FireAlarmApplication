using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations
{
    public class AlertSystemModule : IFireGuardModule
    {
        public string ModuleName => "AlertSystem";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                services.AddDbContext<AlertSystemDbContext>(options =>
                {
                    var connectionString = configuration.GetConnectionString("AlertDB");

                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.UseNetTopologySuite();
                        npgsqlOptions.CommandTimeout(60);
                    });
                });
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            throw new NotImplementedException();
        }

        public Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
