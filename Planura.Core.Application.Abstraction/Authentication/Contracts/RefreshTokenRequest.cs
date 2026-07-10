using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Authentication.Contracts
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
