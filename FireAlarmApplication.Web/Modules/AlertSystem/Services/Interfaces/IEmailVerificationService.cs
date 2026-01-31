namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IEmailVerificationService
    {
        public Task<bool> SendVerificationCodeAsync(string email);
    }
}
