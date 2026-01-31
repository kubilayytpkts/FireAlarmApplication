using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class EmailVerificationService : IEmailVerificationService
    {
        private readonly ILogger<AlertService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IRedisService _redisService;

        public EmailVerificationService(ILogger<AlertService> logger, IConfiguration configuration, IRedisService redisService)
        {
            _logger = logger;
            _configuration = configuration;
            _redisService = redisService;
        }

        public async Task<bool> SendVerificationCodeAsync(string email)
        {
            try
            {
                string code = Generate6DigitCode();

                var subject = "🛰️ STAMS - Email Verification Code";
                var body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 20px;'>
                        <div style='max-width: 600px; margin: 0 auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 40px rgba(0,0,0,0.1);'>
                            
                            <!-- Header -->
                            <div style='background: linear-gradient(135deg, #FF6B6B 0%, #FF8E53 100%); padding: 40px 30px; text-align: center;'>
                                <div style='font-size: 48px; margin-bottom: 10px;'>🛰️</div>
                                <h1 style='color: white; margin: 0; font-size: 28px; font-weight: 700;'>STAMS</h1>
                                <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0; font-size: 14px;'>Satellite Thermal Anomaly Monitoring System</p>
                            </div>
                            
                            <!-- Content -->
                            <div style='padding: 40px 30px;'>
                                <h2 style='color: #333; font-size: 24px; margin: 0 0 20px 0; font-weight: 600;'>
                                    Verify Your Email Address
                                </h2>
                                
                                <p style='color: #666; font-size: 16px; line-height: 1.6; margin: 0 0 30px 0;'>
                                    Welcome to STAMS! To complete your registration and start receiving real-time thermal anomaly alerts from satellite data, 
                                    please verify your email address using the code below:
                                </p>
                                
                                <!-- Verification Code Box -->
                                <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 12px; text-align: center; margin: 30px 0;'>
                                    <p style='color: white; font-size: 14px; margin: 0 0 10px 0; opacity: 0.9; text-transform: uppercase; letter-spacing: 1px;'>
                                        Your Verification Code
                                    </p>
                                    <div style='font-size: 48px; font-weight: 700; color: white; letter-spacing: 12px; font-family: ""Courier New"", monospace;'>
                                        {code}
                                    </div>
                                </div>
                                
                                <!-- Info Box -->
                                <div style='background: #FFF4E6; border-left: 4px solid #FFA726; padding: 16px 20px; border-radius: 8px; margin: 30px 0;'>
                                    <p style='color: #E65100; font-size: 14px; margin: 0; line-height: 1.6;'>
                                        ⚡ <strong>Quick Tip:</strong> This code will expire in <strong>5 minutes</strong>. 
                                        Please enter it in the app as soon as possible.
                                    </p>
                                </div>
                                
                                <p style='color: #666; font-size: 14px; line-height: 1.6; margin: 30px 0 0 0;'>
                                    Once verified, you'll be able to:
                                </p>
                                
                                <ul style='color: #666; font-size: 14px; line-height: 1.8; margin: 10px 0 0 0; padding-left: 20px;'>
                                    <li>🛰️ Monitor thermal anomalies from MTG, VIIRS & MODIS satellites</li>
                                    <li>📍 Track fire detections in real-time with GPS integration</li>
                                    <li>🔔 Receive instant push notifications for nearby anomalies</li>
                                    <li>🗺️ View interactive maps with confidence levels and distances</li>
                                    <li>⚡ Get alerts within 10-20 minutes of satellite detection</li>
                                </ul>
                            </div>
                            
                            <!-- Footer -->
                            <div style='background: #F8F9FA; padding: 30px; text-align: center; border-top: 1px solid #E9ECEF;'>
                                <p style='color: #999; font-size: 13px; margin: 0 0 10px 0;'>
                                    If you didn't create a STAMS account, please ignore this email.
                                </p>
                                <p style='color: #999; font-size: 12px; margin: 10px 0 0 0;'>
                                    © 2025 STAMS - Satellite Thermal Anomaly Monitoring System
                                </p>
                                <p style='color: #999; font-size: 12px; margin: 5px 0 0 0;'>
                                    Powered by MTG, VIIRS & MODIS Satellite Networks
                                </p>
                            </div>
                            
                        </div>
                    </div>
                </body>
                </html>
            ";

                return await SendEmailAsync(email, subject, body, code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification code to {Email}", email);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string code)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var fromEmail = _configuration["Email:FromEmail"]; // örnek: fireguard@outlook.com
                var fromPassword = _configuration["Email:FromPassword"]; // Outlook şifresi (direkt)

                if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(fromPassword))
                {
                    return false;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(fromEmail, fromPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, "STAMS - Satellite Thermal Anomaly Monitoring"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);

                await _redisService.SetAsync($"email_verification:{toEmail}", code, TimeSpan.FromMinutes(5));

                return true;
            }
            catch (SmtpException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private string Generate6DigitCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(4);
            var value = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return value.ToString("D6");
        }
    }
}

