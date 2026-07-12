using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.VendorVerification;

public class RejectVendorDto
{
    public long VendorId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RejectionReason { get; set; } = null!;
}