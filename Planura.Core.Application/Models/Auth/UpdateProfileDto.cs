using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models;

/// <summary>
/// Self-service profile update for any authenticated user's own ApplicationUser fields. Previously
/// nothing served this outside the role-specific profile controllers (ClientController.UpdateMyProfile,
/// VendorController.UpdateMyProfile) - an admin had neither. General-purpose, not admin-specific,
/// even though the Admin Profile page (AdminDashboardPlan.md 2.16) is what first needed it.
/// </summary>
public class UpdateProfileDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = null!;

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(5)]
    public string? PreferredLanguage { get; set; }
}
