using System;
using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Admin
{
    /// <summary>Row shape for the "Admin Accounts" management page (AdminDashboardPlan.md 2.13).</summary>
    public class AdminAccountDto
    {
        public long UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }

    public class CreateAdminDto
    {
        [Required]
        [StringLength(150, MinimumLength = 2)]
        public string FullName { get; set; } = null!;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = null!;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = null!;

        [Required]
        [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
