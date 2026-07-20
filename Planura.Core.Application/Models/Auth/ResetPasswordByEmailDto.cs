using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Auth
{
    public class ResetPasswordByEmailDto : ForgetPasswordByEmailDto
    {
        [Required]
        public required string NewPassword { get; set; }
    }
}
