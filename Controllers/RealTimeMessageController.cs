using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RealTimeMessageController : ControllerBase
    {
        private readonly IRealTimeMessageService _messageService;

        public RealTimeMessageController(IRealTimeMessageService messageService)
        {
            _messageService = messageService;
        }

        /// <summary>
        /// 发送实时消息
        /// </summary>
        /// <remarks>
        /// 发送实时消息给指定接收者。支持三种接收类型：
        /// - User: 发送给特定用户
        /// - Group: 发送给用户组
        /// - All: 发送给所有用户
        /// 
        /// 示例请求:
        /// ```json
        /// {
        ///   "content": "Hello, World!",
        ///   "receiverType": "User",
        ///   "receiverId": "user123"
        /// }
        /// ```
        /// </remarks>
        /// <param name="request">消息请求参数</param>
        /// <returns>发送成功的消息对象</returns>
        /// <response code="200">消息发送成功</response>
        /// <response code="400">请求参数无效</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
        [HttpPost("send")]
        [Authorize]
        [ProducesResponseType(typeof(RealTimeMessage), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            if (request == null) return BadRequest("Request cannot be null");
            if (string.IsNullOrWhiteSpace(request.Content)) 
                return BadRequest("Content cannot be empty");
                
            var message = await _messageService.SendMessageAsync(
                User.Identity?.Name ?? "system",
                request.Content,
                request.ReceiverType,
                request.ReceiverId);

            return Ok(message);
        }

        /// <summary>
        /// 发送带操作的实时消息
        /// </summary>
        /// <remarks>
        /// 发送包含特定操作的实时消息，可用于触发客户端特定行为。
        /// 
        /// 示例请求:
        /// ```json
        /// {
        ///   "content": "请确认操作",
        ///   "actionType": "confirm",
        ///   "actionPayload": "{\"actionId\": \"123\", \"timeout\": 30}",
        ///   "receiverType": "User",
        ///   "receiverId": "user123"
        /// }
        /// ```
        /// </remarks>
        /// <param name="request">带操作的消息请求参数</param>
        /// <returns>发送成功的消息对象</returns>
        /// <response code="200">消息发送成功</response>
        /// <response code="400">请求参数无效</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
        [HttpPost("send-action")]
        [Authorize]
        [ProducesResponseType(typeof(RealTimeMessage), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SendActionMessage([FromBody] SendActionMessageRequest request)
        {
            if (request == null) return BadRequest("Request cannot be null");
            if (string.IsNullOrWhiteSpace(request.Content)) 
                return BadRequest("Content cannot be empty");
            if (string.IsNullOrWhiteSpace(request.ActionType))
                return BadRequest("ActionType cannot be empty");
                
            var message = await _messageService.SendActionMessageAsync(
                User.Identity?.Name ?? "system",
                request.Content,
                request.ActionType,
                request.ActionPayload,
                request.ReceiverType,
                request.ReceiverId);

            return Ok(message);
        }

        /// <summary>
        /// 获取当前用户的未读消息
        /// </summary>
        /// <remarks>
        /// 获取当前登录用户的所有未读消息，包括个人消息和群组消息。
        /// 
        /// 返回的消息按创建时间倒序排列。
        /// </remarks>
        /// <returns>未读消息列表</returns>
        /// <response code="200">成功获取未读消息</response>
        /// <response code="401">未授权访问</response>
        /// <response code="500">服务器内部错误</response>
        [HttpGet("unread")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<RealTimeMessage>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetUnreadMessages()
        {
            var messages = await _messageService.GetUnreadMessagesAsync(User.Identity?.Name ?? "system");
            return Ok(messages);
        }

        /// <summary>
        /// 标记消息为已读
        /// </summary>
        /// <param name="messageId">消息ID</param>
        [HttpPost("mark-read/{messageId}")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead([FromRoute] int messageId)
        {
            await _messageService.MarkAsReadAsync(messageId);
            return Ok();
        }

        /// <summary>
        /// 标记所有消息为已读
        /// </summary>
        [HttpPost("mark-all-read")]
        [Authorize]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _messageService.MarkAllAsReadAsync(User.Identity?.Name ?? "system");
            return Ok();
        }
    }

    public class SendMessageRequest
    {
        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public ReceiverType ReceiverType { get; set; }

        public string? ReceiverId { get; set; }
    }

    public class SendActionMessageRequest
    {
        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public string ActionType { get; set; } = string.Empty;

        public string? ActionPayload { get; set; }

        [Required]
        public ReceiverType ReceiverType { get; set; }

        public string? ReceiverId { get; set; }
    }
}