using System;

namespace HRMSAPI.Services
{
    public interface IPermissionNotificationService
    {
        Task NotifyPermissionChangeAsync(int roleId, string changeType);
        Task NotifyUserPermissionChangeAsync(int userId, int roleId, string changeType);
        Task NotifyAllUsersAsync(string changeType);
        //Task NotifyVersionUpdateAsync(string versionName, string versionCode, string downloadLink);
        Task NotifyAllUsersVersionUpdateAsync();
        Task LogoutUser(string userId);
    }
} 