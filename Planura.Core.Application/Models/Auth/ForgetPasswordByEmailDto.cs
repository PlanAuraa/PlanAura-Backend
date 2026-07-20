using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Models.Auth
{
    public class ForgetPasswordByEmailDto
    {
        [Required]
        public required string Email { get; set; }
    }
}
