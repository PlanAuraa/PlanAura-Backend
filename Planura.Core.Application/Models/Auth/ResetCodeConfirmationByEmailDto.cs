using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Auth
{
    public class ResetCodeConfirmationByEmailDto : ForgetPasswordByEmailDto
    {
        [Required]
        public required int ResetCode { get; set; }
    }
}
