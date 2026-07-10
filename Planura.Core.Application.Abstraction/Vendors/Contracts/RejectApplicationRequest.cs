using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class RejectApplicationRequest
    {
        [Required, StringLength(1000, MinimumLength = 3)]
        public string Reason { get; set; } = null!;
    }
}
