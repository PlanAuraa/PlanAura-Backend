using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorRegisterRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = null!;

        [Required, MinLength(8), StringLength(100)]
        public string Password { get; set; } = null!;

        [Required, StringLength(200)]
        public string FullName { get; set; } = null!;

        [Required, Phone, StringLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        public BusinessInfoDto BusinessInfo { get; set; } = null!;

        [Required]
        public LocationDto Location { get; set; } = null!;

        public List<VendorDocumentUpload> Documents { get; set; } = new();

        public List<UploadedFileInfo> Images { get; set; } = new();
    }
}
