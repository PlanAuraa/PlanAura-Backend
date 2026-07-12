using System;

namespace Planura.Core.Application.Models.Notification
{
    public class NotificationDto
    {
        public long Id { get; set; }

        public string? Type { get; set; }

        public string? Title { get; set; }

        public string? Body { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
