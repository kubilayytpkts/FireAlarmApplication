using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Net;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManagementDbContext _userManagementDbContext;
        private readonly IRedisService _redisService;
        private readonly ILogger<UserManagementService> _logger;
        public UserManagementService(UserManagementDbContext userManagementDbContext, IRedisService redisService, ILogger<UserManagementService> logger)
        {
            _userManagementDbContext = userManagementDbContext;
            _redisService = redisService;
            _logger = logger;
        }
        public async Task<ServiceResponse> Register([FromBody] RegisterRequest request)
        {
            try
            {
                //email kontrolü
                var existingUser = await _userManagementDbContext.Users.AnyAsync(x => x.Email == request.Email);
                if (existingUser)
                    return new ServiceResponse
                    {
                        Message = "Email already registered",
                        StatusCode = HttpStatusCode.BadRequest,
                        Success = false,
                    };
                // yeni kullanıcı oluşturma
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = UserRole.Civilian,// Default role
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    IsLocationTrackingEnabled = true,
                    EnableEmailNotification = true,
                    EnablePushNotification = true,
                    EnableSmsNotification = !string.IsNullOrEmpty(request.PhoneNumber)
                };

                // İlk konum bilgisi varsa ekle
                if (request.InitialLocation != null)
                {
                    user.CurrentLocation = new Point(request.InitialLocation.Longitude, request.InitialLocation.Latitude) { SRID = 4326 };
                    user.LastLocationUpdate = DateTime.UtcNow;
                }
                _userManagementDbContext.Add(user);
                await _userManagementDbContext.SaveChangesAsync();

                _logger.LogInformation("New user registered: {Email}", user.Email);

                return new ServiceResponse
                {
                    Message = "Registiration Successful",
                    StatusCode = HttpStatusCode.OK,
                    Success = true,
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new ServiceResponse { Success = false, StatusCode = HttpStatusCode.InternalServerError, Message = $"Error during user registration:{ex.Message}" };
            }
        }

        public Task<IActionResult> BatchLocationUpdate([FromBody] List<LocationUpdateRequest> locations)
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> GetLocation()
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> ToggleTracking([FromBody] TrackingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> UpdateLocation([FromBody] LocationUpdateRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
