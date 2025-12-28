using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DynamicDbApi.Hubs
{
    public class MessageHub : Hub
    {
        // 存储用户ID和连接ID的映射关系
        private static readonly ConcurrentDictionary<string, string> _userConnections = new ConcurrentDictionary<string, string>();
        // 存储群聊ID和用户ID列表的映射关系
        private static readonly ConcurrentDictionary<string, List<string>> _chatGroups = new ConcurrentDictionary<string, List<string>>();

        // 客户端连接时调用
        public override async Task OnConnectedAsync()
        {
            string? userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                _userConnections[userId] = Context.ConnectionId;
                await Clients.Caller.SendAsync("Connected", new { Message = "Connected successfully" });
            }
            await base.OnConnectedAsync();
        }

        // 客户端断开连接时调用
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var disconnectedUser = _userConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(disconnectedUser.Key))
            {
                _userConnections.TryRemove(disconnectedUser.Key, out _);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // 加入群聊
        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
            
            // 记录群聊成员
            _chatGroups.AddOrUpdate(groupId, 
                new List<string> { Context.ConnectionId }, 
                (key, existing) => { existing.Add(Context.ConnectionId); return existing; });
        }

        // 离开群聊
        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
            
            // 从群聊成员列表中移除
            if (_chatGroups.TryGetValue(groupId, out var members))
            {
                members.Remove(Context.ConnectionId);
            }
        }

        // 获取用户连接ID（供服务层调用）
        public static string? GetUserConnectionId(string userId)
        {
            _userConnections.TryGetValue(userId, out string? connectionId);
            return connectionId;
        }

        // 获取群聊成员连接ID列表（供服务层调用）
        public static List<string>? GetGroupMembers(string groupId)
        {
            _chatGroups.TryGetValue(groupId, out List<string>? members);
            return members;
        }

        // 向特定群聊发送消息（供客户端直接调用）
        public async Task SendToGroup(string groupId, string content)
        {
            string? senderId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? "anonymous";
            
            var message = new {
                SenderId = senderId,
                Content = content,
                GroupId = groupId,
                Timestamp = DateTime.UtcNow
            };
            
            await Clients.Group(groupId).SendAsync("ReceiveGroupMessage", message);
        }
    }
}