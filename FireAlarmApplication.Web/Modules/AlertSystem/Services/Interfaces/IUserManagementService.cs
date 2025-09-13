using FireAlarmApplication.Shared.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace FireAlarmApplication.Web.Modules.AlertSystem.Services.Interfaces
{
    public interface IUserManagementService
    {
        public Task<ServiceResponse<int>> Register([FromBody] RegisterRequest request); // Yeni kullanıcı kaydı
        public Task<ServiceResponse<User>> Login([FromBody] LoginRequest request); // Kullanıcı girişi
        public Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request); // Kullanıcı profil güncelleme
        public Task<IActionResult> UpdateLocation([FromBody] LocationUpdateRequest request); // Konum güncelle
        public Task<IActionResult> BatchLocationUpdate([FromBody] List<LocationUpdateRequest> locations); // Toplu konum güncelleme (offline'dan online'a geçişte)
        public Task<IActionResult> GetLocation(); // Kullanıcının konum bilgilerini getir
        public Task<IActionResult> ToggleTracking([FromBody] TrackingRequest request); // Konum takibini aç/kapat
    }
}
