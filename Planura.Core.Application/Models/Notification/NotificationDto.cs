using System;

namespace Planura.Core.Application.Models.Notification
{
    public class NotificationDto
    {
        public long Id { get; set; }

        public string? Type { get; set; }

        public string? Title { get; set; }

        public string? Body { get; set; }

        /// <summary>Optional JSON payload (e.g. download link, booking/vendor ids) for rendering notification actions client-side.</summary>
        public string? DataJson { get; set; }

        public bool IsRead { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
