using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Notification
{
    public class BroadcastNotificationDto
    {
        /// <summary>"client", "vendor", or "all".</summary>
        [Required]
        public string Role { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(1000)]
        public string? Body { get; set; }
    }
}
