using DynamicDbApi.Models;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;
using DynamicDbApi.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public class RealTimeMessageService : IRealTimeMessageService
    {
        private readonly ISqlSugarClient _db;
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly ILogger<RealTimeMessageService> _logger;

        public RealTimeMessageService(
            ISqlSugarClient db, 
            IHubContext<MessageHub> hubContext,
            ILogger<RealTimeMessageService> logger)
        {
            _db = db;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<RealTimeMessage?> SendMessageAsync(string senderId, string content, 
            ReceiverType receiverType, string? receiverId = null)
        {
            // 输入验证
            if (string.IsNullOrWhiteSpace(senderId))
                throw new ArgumentException("SenderId cannot be null or empty", nameof(senderId));
            
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            if (content.Length > 1000)
                throw new ArgumentException("Content cannot exceed 1000 characters", nameof(content));
            
            // 验证接收者ID（如果指定了特定接收者）
            if (receiverType != ReceiverType.All && string.IsNullOrWhiteSpace(receiverId))
                throw new ArgumentException("ReceiverId is required for specific receiver types", nameof(receiverId));

            var message = new RealTimeMessage
            {
                SenderId = senderId,
                Content = content,
                ReceiverType = (int)receiverType,
                ReceiverId = receiverId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _db.Insertable(message).ExecuteReturnEntityAsync();
                await NotifyClientsAsync(message);
                
                _logger.LogInformation($"Message sent successfully from {senderId} to {receiverType}:{receiverId}");
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send message from {senderId}");
                throw new InvalidOperationException($"Failed to send message: {ex.Message}", ex);
            }
        }

        public async Task<RealTimeMessage?> SendActionMessageAsync(string senderId, string content, 
            string actionType, string? actionPayload, ReceiverType receiverType, 
            string? receiverId = null)
        {
            // 输入验证
            if (string.IsNullOrWhiteSpace(senderId))
                throw new ArgumentException("SenderId cannot be null or empty", nameof(senderId));
            
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            
            if (string.IsNullOrWhiteSpace(actionType))
                throw new ArgumentException("ActionType cannot be null or empty", nameof(actionType));
            
            if (content.Length > 1000)
                throw new ArgumentException("Content cannot exceed 1000 characters", nameof(content));
            
            if (actionType.Length > 50)
                throw new ArgumentException("ActionType cannot exceed 50 characters", nameof(actionType));
            
            if (actionPayload?.Length > 500)
                throw new ArgumentException("ActionPayload cannot exceed 500 characters", nameof(actionPayload));
            
            // 验证接收者ID（如果指定了特定接收者）
            if (receiverType != ReceiverType.All && string.IsNullOrWhiteSpace(receiverId))
                throw new ArgumentException("ReceiverId is required for specific receiver types", nameof(receiverId));

            var message = new RealTimeMessage
            {
                SenderId = senderId,
                Content = content,
                ReceiverType = (int)receiverType,
                ReceiverId = receiverId,
                ActionType = actionType,
                ActionPayload = actionPayload,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _db.Insertable(message).ExecuteReturnEntityAsync();
                await NotifyClientsAsync(message);
                
                _logger.LogInformation($"Action message sent successfully from {senderId} with action {actionType}");
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send action message from {senderId}");
                throw new InvalidOperationException($"Failed to send action message: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<RealTimeMessage>> GetUnreadMessagesAsync(string userId)
        {
            return await _db.Queryable<RealTimeMessage>()
                .Where(m => !m.IsRead && 
                    (m.ReceiverType == (int)ReceiverType.All || 
                     m.ReceiverType == (int)ReceiverType.User && m.ReceiverId == userId ||
                     m.ReceiverType == (int)ReceiverType.Group && 
                        _db.Queryable<UserGroup>().Any(g => g.GroupId == m.ReceiverId && g.UserId == userId)))
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int messageId)
        {
            await _db.Updateable<RealTimeMessage>()
                .SetColumns(m => m.IsRead == true)
                .Where(m => m.Id == messageId)
                .ExecuteCommandAsync();
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            await _db.Updateable<RealTimeMessage>()
                .SetColumns(m => m.IsRead == true)
                .Where(m => !m.IsRead && 
                    (m.ReceiverType == (int)ReceiverType.User && m.ReceiverId == userId ||
                     m.ReceiverType == (int)ReceiverType.Group && 
                        _db.Queryable<UserGroup>().Any(g => g.GroupId == m.ReceiverId && g.UserId == userId)))
                .ExecuteCommandAsync();
        }

        private async Task NotifyClientsAsync(RealTimeMessage message)
        {
            try
            {
                switch ((ReceiverType)message.ReceiverType)
                {
                    case ReceiverType.User:
                        // 单对单消息发送
                        if (!string.IsNullOrEmpty(message.ReceiverId))
                        {
                            // 先尝试通过SignalR的User方法发送
                            await _hubContext.Clients.User(message.ReceiverId)
                                .SendAsync("ReceiveMessage", message);
                            
                            // 再通过我们自定义的连接映射发送，确保消息送达
                            string? connectionId = MessageHub.GetUserConnectionId(message.ReceiverId);
                            if (!string.IsNullOrEmpty(connectionId))
                            {
                                await _hubContext.Clients.Client(connectionId)
                                    .SendAsync("ReceiveMessage", message);
                            }
                            
                            _logger.LogInformation($"Direct message sent to user {message.ReceiverId}");
                        }
                        break;
                    case ReceiverType.Group:
                        // 用户组消息发送
                        if (!string.IsNullOrEmpty(message.ReceiverId))
                        {
                            // 获取组内所有用户
                            var groupUsers = await GetGroupUsersAsync(message.ReceiverId);
                            int sentCount = 0;
                            
                            foreach (string userId in groupUsers)
                            {
                                try
                                {
                                    // 向组内每个用户发送消息
                                    string? connectionId = MessageHub.GetUserConnectionId(userId);
                                    if (!string.IsNullOrEmpty(connectionId))
                                    {
                                        await _hubContext.Clients.Client(connectionId)
                                            .SendAsync("ReceiveGroupMessage", message);
                                        sentCount++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, $"Failed to send message to user {userId} in group {message.ReceiverId}");
                                }
                            }
                            
                            _logger.LogInformation($"User group message sent to group {message.ReceiverId}, delivered to {sentCount} of {groupUsers.Count()} users");
                        }
                        break;
                    case ReceiverType.All:
                        // 广播消息发送
                        // 首先使用SignalR的All方法进行广播
                        await _hubContext.Clients.All
                            .SendAsync("ReceiveMessage", message);
                        
                        // 广播消息功能已完成，消息已发送给所有连接的客户端
                        _logger.LogInformation($"Broadcast message sent to all connected clients");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying clients about new message");
            }
        }

        // 用户组管理方法实现
        public async Task AddUserToGroupAsync(string userId, string groupId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
            if (string.IsNullOrWhiteSpace(groupId))
                throw new ArgumentException("GroupId cannot be null or empty", nameof(groupId));

            // 检查是否已存在
            var existing = await _db.Queryable<UserGroup>()
                .Where(ug => ug.UserId == userId && ug.GroupId == groupId)
                .FirstAsync();

            if (existing == null)
            {
                var userGroup = new UserGroup
                {
                    UserId = userId,
                    GroupId = groupId
                };

                await _db.Insertable(userGroup).ExecuteCommandAsync();
                _logger.LogInformation($"User {userId} added to group {groupId}");
            }
        }

        public async Task RemoveUserFromGroupAsync(string userId, string groupId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId cannot be null or empty", nameof(userId));
            if (string.IsNullOrWhiteSpace(groupId))
                throw new ArgumentException("GroupId cannot be null or empty", nameof(groupId));

            await _db.Deleteable<UserGroup>()
                .Where(ug => ug.UserId == userId && ug.GroupId == groupId)
                .ExecuteCommandAsync();

            _logger.LogInformation($"User {userId} removed from group {groupId}");
        }

        public async Task<IEnumerable<string>> GetGroupUsersAsync(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new ArgumentException("GroupId cannot be null or empty", nameof(groupId));

            return await _db.Queryable<UserGroup>()
                .Where(ug => ug.GroupId == groupId)
                .Select(ug => ug.UserId)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetUserGroupsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId cannot be null or empty", nameof(userId));

            return await _db.Queryable<UserGroup>()
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.GroupId)
                .ToListAsync();
        }
    }
}