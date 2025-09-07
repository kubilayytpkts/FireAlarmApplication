using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class AlertRuleService : IAlertRuleService
    {
        public Task<AlertRule> CreateOrUpdateRuleAsync(AlertRule alertRule)
        {
            throw new NotImplementedException();
        }

        public Task<AlertRule?> FindApplicableRuleAsync(UserRole userRole, double distance, double confidence)
        {
            throw new NotImplementedException();
        }

        public string GenerateMessageFromTemplate(string template, Dictionary<string, object> placeHolders)
        {
            throw new NotImplementedException();
        }

        public Task<List<AlertRule>> GetActiveRulesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<AlertRule?> GetRuleForUserRoleAsync(UserRole userRole)
        {
            throw new NotImplementedException();
        }
    }
}
