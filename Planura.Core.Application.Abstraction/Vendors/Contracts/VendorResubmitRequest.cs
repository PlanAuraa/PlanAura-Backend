using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VendorResubmitRequest
    {
        [Required, MinLength(1)]
        public List<VendorDocumentUpload> Documents { get; set; } = new();
    }
}
