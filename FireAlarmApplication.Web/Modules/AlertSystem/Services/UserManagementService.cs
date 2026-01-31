using FireAlarmApplication.Shared.Contracts.Enums;
using FireAlarmApplication.Shared.Contracts.Models;
using FireAlarmApplication.Web.Modules.AlertSystem.Data;
using FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces;
using FireAlarmApplication.Web.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Geometries;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManagementDbContext _userManagementDbContext;
        private readonly IRedisService _redisService;
        private readonly ILogger<UserManagementService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailVerificationService _emailVerificationService;
        public UserManagementService(
            UserManagementDbContext userManagementDbContext,
            IRedisService redisService,
            ILogger<UserManagementService> logger,
            IConfiguration configuration,
            IEmailVerificationService emailVerificationService)
        {
            _userManagementDbContext = userManagementDbContext;
            _redisService = redisService;
            _logger = logger;
            _configuration = configuration;
            _emailVerificationService = emailVerificationService;
        }

        public async Task<ServiceResponse<int>> Register(RegisterRequest request)
        {
            try
            {
                // Email kontrolü
                var existingUser = await _userManagementDbContext.Users
                    .AnyAsync(x => x.Email == request.Email);

                if (existingUser)
                {
                    return new ServiceResponse<int>
                    {
                        Message = "Email already registered",
                        StatusCode = HttpStatusCode.BadRequest,
                        Success = false,
                    };
                }

                // Yeni kullanıcı oluştur
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = UserRole.Civilian,
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
                    user.CurrentLocation = new Point(
                        request.InitialLocation.Longitude,
                        request.InitialLocation.Latitude)
                    { SRID = 4326 };
                    user.LastLocationUpdate = DateTime.UtcNow;
                }

                _userManagementDbContext.Users.Add(user);
                await _userManagementDbContext.SaveChangesAsync();

                var sendEmailResult = await _emailVerificationService.SendVerificationCodeAsync(user.Email);
                if (sendEmailResult)
                {
                    return new ServiceResponse<int>
                    {
                        Message = "Please enter the code sent to your email address.",
                        StatusCode = HttpStatusCode.OK,
                        Success = true,
                        Data = 1
                    };
                }
                else
                {
                    return new ServiceResponse<int>
                    {
                        Message = "Operation failed, please try again later.",
                        StatusCode = HttpStatusCode.InternalServerError,
                        Success = true,
                        Data = 1
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new ServiceResponse<int>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error during user registration: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<LoginResponse>> Login(LoginRequest request)
        {
            try
            {
                var user = await _userManagementDbContext.Users
                    .FirstOrDefaultAsync(x => x.Email == request.Email && x.IsActive);

                if (user != null && user.IsEmailVerified == false)
                {
                    await _emailVerificationService.SendVerificationCodeAsync(user.Email);

                    return new ServiceResponse<LoginResponse>
                    {
                        Message = "Please verify your identity using the code sent to your email address.",
                        StatusCode = HttpStatusCode.Forbidden,
                        Success = false,
                        Data = new LoginResponse
                        {
                            Email = request.Email,
                            RequiresVerification = true
                        }
                    };
                }

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return new ServiceResponse<LoginResponse>
                    {
                        Message = "Invalid credentials",
                        StatusCode = HttpStatusCode.Unauthorized,
                        Success = false,
                    };
                }

                // Son giriş zamanını güncelle
                user.LastLoginAt = DateTime.UtcNow;

                // Push token'ları güncelle
                if (!string.IsNullOrEmpty(request.FcmToken))
                    user.FcmToken = request.FcmToken;
                if (!string.IsNullOrEmpty(request.ApnsToken))
                    user.ApnsToken = request.ApnsToken;

                await _userManagementDbContext.SaveChangesAsync();

                // JWT token oluştur
                var token = GenerateJwtToken(user);

                return new ServiceResponse<LoginResponse>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Data = new LoginResponse
                    {
                        Token = token,
                        UserId = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role.ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return new ServiceResponse<LoginResponse>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Login failed: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<bool>> UpdateProfile(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                // Profil bilgilerini güncelle
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;
                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber;

                // Bildirim tercihlerini güncelle
                user.EnableEmailNotification = request.EnableEmail;
                user.EnableSmsNotification = request.EnableSms;
                user.EnablePushNotification = request.EnablePush;

                await _userManagementDbContext.SaveChangesAsync();

                _logger.LogInformation("Profile updated for user: {UserId}", userId);

                return new ServiceResponse<bool>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Profile updated successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user: {UserId}", userId);
                return new ServiceResponse<bool>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error updating profile: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<bool>> UpdateLocation(Guid userId, LocationUpdateRequest request)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                // Türkiye sınırları kontrolü
                //if (!IsValidTurkeyLocation(request.Latitude, request.longitude))
                //{
                //    return new ServiceResponse<bool>
                //    {
                //        Success = false,
                //        StatusCode = HttpStatusCode.BadRequest,
                //        Message = "Location outside Turkey boundaries"
                //    };
                //}

                var newLocation = new Point(request.longitude, request.Latitude) { SRID = 4326 };

                // Önceki konumla mesafe kontrolü
                if (user.CurrentLocation != null)
                {
                    var distance = CalculateDistance(
                        user.CurrentLocation.Y, user.CurrentLocation.X,
                        request.Latitude, request.longitude);

                    // 50 metreden az hareket varsa güncelleme yapma
                    if (distance < 0.05)
                    {
                        return new ServiceResponse<bool>
                        {
                            Success = true,
                            StatusCode = HttpStatusCode.OK,
                            Message = "Location unchanged (movement < 50m)",
                            Data = true
                        };
                    }
                }

                // Konum güncelle
                user.CurrentLocation = newLocation;
                user.LastLocationUpdate = DateTime.UtcNow;
                user.LocationAccuracy = request.Accuracy;

                // Ev konumu yoksa ve kullanıcı durağansa ev konumu olarak kaydet
                if (user.HomeLocation == null && request.Speed < 0.5)
                {
                    user.HomeLocation = newLocation;
                    _logger.LogInformation("Home location auto-set for user {UserId}", userId);
                }

                await _userManagementDbContext.SaveChangesAsync();

                // Redis'e cache'le
                await CacheUserLocation(user);

                _logger.LogInformation("Location updated for user {UserId}: ({Lat}, {Lng})",
                    userId, request.Latitude, request.longitude);

                return new ServiceResponse<bool>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Location updated successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location for user: {UserId}", userId);
                return new ServiceResponse<bool>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error updating location: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<int>> BatchLocationUpdate(Guid userId, List<LocationUpdateRequest> locations)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<int>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                // En son konumu al (zaman damgasına göre)
                var latestLocation = locations
                    .Where(l => IsValidTurkeyLocation(l.Latitude, l.longitude))
                    .OrderByDescending(l => l.TimeStamp)
                    .FirstOrDefault();

                if (latestLocation != null)
                {
                    user.CurrentLocation = new Point(latestLocation.longitude, latestLocation.Latitude) { SRID = 4326 };
                    user.LastLocationUpdate = latestLocation.TimeStamp ?? DateTime.UtcNow;
                    user.LocationAccuracy = latestLocation.Accuracy;

                    await _userManagementDbContext.SaveChangesAsync();
                    await CacheUserLocation(user);
                }

                return new ServiceResponse<int>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Batch update completed",
                    Data = locations.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch location update for user: {UserId}", userId);
                return new ServiceResponse<int>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Batch update failed: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<LocationResponse>> GetLocation(Guid userId)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<LocationResponse>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                return new ServiceResponse<LocationResponse>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Data = new LocationResponse
                    {
                        //HasLocation = user.HasValidLocation,
                        CurrentLatitude = user.CurrentLocation?.Y,
                        CurrentLongitude = user.CurrentLocation?.X,
                        HomeLatitude = user.HomeLocation?.Y,
                        HomeLongitude = user.HomeLocation?.X,
                        LocationAccuracy = user.LocationAccuracy,
                        LastLocationUpdate = user.LastLocationUpdate,
                        TrackingEnabled = user.IsLocationTrackingEnabled
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location for user: {UserId}", userId);
                return new ServiceResponse<LocationResponse>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Failed to get location: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<bool>> ToggleTracking(Guid userId, TrackingRequest request)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                user.IsLocationTrackingEnabled = request.Enable;
                await _userManagementDbContext.SaveChangesAsync();

                // Cache'den temizle
                if (!request.Enable)
                {
                    await _redisService.RemoveAsync($"user_location:{userId}");
                }

                _logger.LogInformation("Location tracking {Status} for user {UserId}",
                    request.Enable ? "enabled" : "disabled", userId);

                return new ServiceResponse<bool>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = $"Location tracking {(request.Enable ? "enabled" : "disabled")}",
                    Data = request.Enable
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling tracking for user: {UserId}", userId);
                return new ServiceResponse<bool>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Failed to toggle tracking: {ex.Message}"
                };
            }
        }
        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            return await _userManagementDbContext.Users.FindAsync(userId);
        }
        public async Task<List<User>> FindUsersInRadiusAsync(double lat, double lng, double radiusKm)
        {
            try
            {
                var point = new Point(lng, lat) { SRID = 4326 };
                var radiusMeters = radiusKm * 1000;

                // PostGIS ST_DWithin kullanarak spatial query
                var users = await _userManagementDbContext.Users
                    .Where(u => u.IsActive)
                    .Where(u => u.IsLocationTrackingEnabled)
                    .Where(u =>
                        (u.CurrentLocation != null && u.CurrentLocation.IsWithinDistance(point, radiusMeters)) ||
                        (u.HomeLocation != null && u.HomeLocation.IsWithinDistance(point, radiusMeters)))
                    .ToListAsync();

                _logger.LogInformation("Found {Count} users within {Radius}km of ({Lat}, {Lng})",
                    users.Count, radiusKm, lat, lng);

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding users in radius");
                return new List<User>();
            }
        }
        public async Task<ServiceResponse<bool>> UpdateUserPassword(Guid userId, UpdateUserPasswordRequest userRequest)
        {
            try
            {
                var user = await _userManagementDbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                if (user == null || !BCrypt.Net.BCrypt.Verify(userRequest.UserPassword, user.PasswordHash))
                {
                    return new ServiceResponse<bool>
                    {
                        Message = "User password is incorret !",
                        StatusCode = HttpStatusCode.BadRequest,
                        Success = false,
                    };
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(userRequest.NewUserPassword);
                _userManagementDbContext.Update(user);
                _userManagementDbContext.SaveChanges();

                return new ServiceResponse<bool>
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "User password update successful."
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return new ServiceResponse<bool>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"User password update is failed: {ex.Message}"
                };
            }
        }
        public async Task<ServiceResponse<UserInformation>> GetUserInformation(Guid userId)
        {
            try
            {
                if (userId == null)
                {
                    return new ServiceResponse<UserInformation>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                var user = await _userManagementDbContext.Users.FindAsync(userId);

                if (user == null)
                {
                    return new ServiceResponse<UserInformation>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "User not found"
                    };
                }

                UserInformation userInfo = new UserInformation
                {
                    createdAt = user.CreatedAt,
                    Email = user.Email,
                    enableEmailNotification = user.EnableEmailNotification,
                    enablePushNotification = user.EnablePushNotification,
                    enableSmsNotification = user.EnableSmsNotification,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    UserId = user.Id
                };

                return new ServiceResponse<UserInformation>
                {
                    Data = userInfo,
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Success"
                };
            }
            catch (Exception)
            {

                throw;
            }
        }
        public async Task<ServiceResponse<bool>> VerifyUserEmailCode(string email, string code)
        {
            try
            {
                var keyCode = await _redisService.GetAsync<string>($"email_verification:{email}");
                if (string.IsNullOrEmpty(keyCode.ToString()))
                {
                    return new ServiceResponse<bool>
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "You entered the wrong code or the code you entered has expired.",
                    };
                }
                else
                {
                    if (keyCode == code)
                    {
                        var user = await _userManagementDbContext.Users.FirstOrDefaultAsync(x => x.Email == email);

                        if (user == null) return new ServiceResponse<bool> { Message = "User not found" };

                        user.IsEmailVerified = true;
                        _userManagementDbContext.Update(user);
                        await _userManagementDbContext.SaveChangesAsync();

                        await _redisService.RemoveAsync($"email_verification:{email}");

                        return new ServiceResponse<bool>
                        {
                            Success = true,
                            StatusCode = HttpStatusCode.OK,
                            Message = "verification is process successful",
                        };


                    }
                    else
                    {
                        return new ServiceResponse<bool>
                        {
                            Success = false,
                            StatusCode = HttpStatusCode.BadRequest,
                            Message = "You entered the wrong code or the code you entered has expired.",
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ServiceResponse<bool>
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"{ex.Message}",
                };
            }
        }

        #region Helper Methods
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "fZ7@Qp1!vL4$rT9#xW2^mB8&nH6*kD3%Gy5+Jc0?SaEeUvYwRjFhZtPqLsMdNb");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("userId", user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        private bool IsValidTurkeyLocation(double latitude, double longitude)
        {
            return latitude >= 36 && latitude <= 42 &&
                   longitude >= 26 && longitude <= 45;
        }
        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        private async Task CacheUserLocation(User user)
        {
            //if (user.HasValidLocation)
            //{
            var locationInfo = new UserLocationInfo
            {
                UserId = user.Id,
                Latitude = user.Latitude ?? 0,
                Longitude = user.Longitude ?? 0,
                UserRole = user.Role,
                LastUpdated = user.LastLocationUpdate ?? DateTime.UtcNow,
                IsActive = user.IsActive
            };

            await _redisService.SetAsync(
                $"user_location:{user.Id}",
                locationInfo,
                TimeSpan.FromMinutes(30));
            //}
        }
        #endregion
    }
}