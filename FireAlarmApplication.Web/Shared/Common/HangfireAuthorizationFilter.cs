using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace FireAlarmApplication.Web.Shared.Common
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Development'ta herkese açık
            if (httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                return true;
            }
            // Production'da authentication gerekli
            // Şimdilik false, sonra authentication ekleyeceğiz
            return false;

        }
    }
}
