using System;
using Microsoft.AspNetCore.SignalR;

namespace HRMSAPI.Hubs
{
    public class PermissionHub : Hub
    {
        public async Task JoinRoleGroup(int roleId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{roleId}");
        }

        public async Task LeaveRoleGroup(int roleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Role_{roleId}");
        }

        public async Task JoinUserGroup(int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task LeaveUserGroup(int userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task JoinUpdateGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "UpdateGroup");
        }

        public async Task LeaveUpdateGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "UpdateGroup");
        }
        public async Task JoinLogoutGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "LogoutGroup");
        }

        public async Task LeaveLogoutGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "LogoutGroup");
        }
    }
} 