using System;

namespace DynamicDbApi.Models
{
    public class RealTimeMessage
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = null!;
        public string Content { get; set; } = null!;
        public int ReceiverType { get; set; }
        public string? ReceiverId { get; set; }
        public string? ActionType { get; set; }
        public string? ActionPayload { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}