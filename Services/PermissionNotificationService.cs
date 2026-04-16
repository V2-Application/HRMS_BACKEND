using System;
using HRMSAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HRMSAPI.Services
{
    public class PermissionNotificationService : IPermissionNotificationService
    {
        private readonly IHubContext<PermissionHub> _hubContext;

        public PermissionNotificationService(IHubContext<PermissionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyPermissionChangeAsync(int roleId, string changeType)
        {
            // Notify all users with this role
            await _hubContext.Clients.Group($"Role_{roleId}").SendAsync("PermissionChanged", new
            {
                RoleId = roleId,
                ChangeType = changeType,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyUserPermissionChangeAsync(int userId, int roleId, string changeType)
        {
            // Notify specific user
            await _hubContext.Clients.Group($"User_{userId}").SendAsync("UserPermissionChanged", new
            {
                UserId = userId,
                RoleId = roleId,
                ChangeType = changeType,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyAllUsersAsync(string changeType)
        {
            // Notify all connected users
            await _hubContext.Clients.All.SendAsync("GlobalPermissionChanged", new
            {
                ChangeType = changeType,
                Timestamp = DateTime.UtcNow
            });
        }
        public async Task LogoutUser(string userId) {
            await _hubContext.Clients.All.SendAsync("Logout", new
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow
            });
        }

        //public async Task NotifyVersionUpdateAsync(string versionName, string versionCode, string downloadLink)
        //{
        //    // Notify all users in the update group about new version
        //    await _hubContext.Clients.Group("UpdateGroup").SendAsync("VersionUpdateAvailable", new
        //    {
        //        VersionName = versionName,
        //        VersionCode = versionCode,
        //        DownloadLink = downloadLink,
        //        Timestamp = DateTime.UtcNow,
        //        Message = "A new version is available! Click 'Update Now' to refresh your browser."
        //    });
        //}

        public async Task NotifyAllUsersVersionUpdateAsync()
        {
            // Notify all connected users about version update
            await _hubContext.Clients.All.SendAsync("VersionUpdateAvailable", new
            {
                Timestamp = DateTime.UtcNow,
                Message = "A new version is available! Click 'Update Now' to refresh your browser."
            });
        }
    }
} 