using DynamicDbApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public interface IRealTimeMessageService
    {
        Task<RealTimeMessage?> SendMessageAsync(string senderId, string content, 
            ReceiverType receiverType, string? receiverId = null);
            
        Task<RealTimeMessage?> SendActionMessageAsync(string senderId, string content, 
            string actionType, string? actionPayload, ReceiverType receiverType, 
            string? receiverId = null);
            
        Task<IEnumerable<RealTimeMessage>> GetUnreadMessagesAsync(string userId);
        Task MarkAsReadAsync(int messageId);
        Task MarkAllAsReadAsync(string userId);
        
        // 用户组管理方法
        Task AddUserToGroupAsync(string userId, string groupId);
        Task RemoveUserFromGroupAsync(string userId, string groupId);
        Task<IEnumerable<string>> GetGroupUsersAsync(string groupId);
        Task<IEnumerable<string>> GetUserGroupsAsync(string userId);
    }

    public enum ReceiverType
    {
        User,
        Group,
        All
    }
}