using FireAlarmApplication.Shared.Contracts.Models;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<ServiceResponse<UserInformation>> GetUserInformation(Guid userId);
        Task<ServiceResponse<int>> Register(RegisterRequest request);
        Task<ServiceResponse<LoginResponse>> Login(LoginRequest request);
        Task<ServiceResponse<bool>> UpdateProfile(Guid userId, UpdateProfileRequest request);
        Task<ServiceResponse<bool>> UpdateLocation(Guid userId, LocationUpdateRequest request);
        Task<ServiceResponse<int>> BatchLocationUpdate(Guid userId, List<LocationUpdateRequest> locations);
        Task<ServiceResponse<LocationResponse>> GetLocation(Guid userId);
        Task<ServiceResponse<bool>> ToggleTracking(Guid userId, TrackingRequest request);
        Task<User?> GetUserByIdAsync(Guid userId);
        Task<List<User>> FindUsersInRadiusAsync(double lat, double lng, double radiusKm);
        Task<ServiceResponse<bool>> UpdateUserPassword(Guid userId, UpdateUserPasswordRequest updatePasswordRequest);
        Task<ServiceResponse<bool>> VerifyUserEmailCode(string email, string code);
    }
}
