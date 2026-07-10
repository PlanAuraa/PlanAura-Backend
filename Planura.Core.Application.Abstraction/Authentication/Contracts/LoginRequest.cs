using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Authentication.Contracts
{
    public class LoginRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }
}
