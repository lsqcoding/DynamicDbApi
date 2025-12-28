using System;

namespace DynamicDbApi.Models
{
    public class MailServer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; } = true;
        public string? DefaultFrom { get; set; }
        public string? DisplayName { get; set; }
        public bool IsDefault { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}