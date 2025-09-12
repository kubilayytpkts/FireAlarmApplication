using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Main_Operations
{
    public class UserManagementModule : IFireGuardModule
    {
        public string ModuleName => throw new NotImplementedException();

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                services.AddDbContext<UserManagementDbContext>(options =>
                {
                    var connectionString = configuration.GetConnectionString("DefaultConnection");

                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.UseNetTopologySuite();
                        npgsqlOptions.UseNetTopologySuite();
                        npgsqlOptions.CommandTimeout(60);
                    });
                });
                services.AddScoped<IUserManagementService, UserManagementService>();
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
