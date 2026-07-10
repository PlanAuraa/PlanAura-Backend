using System.ComponentModel.DataAnnotations;

namespace Planura.Core.Application.Abstraction.Authentication.Contracts
{
    public class CustomerRegisterRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = null!;

        [Required, MinLength(8), StringLength(100)]
        public string Password { get; set; } = null!;

        [Required, StringLength(200)]
        public string FullName { get; set; } = null!;

        [Required, Phone, StringLength(20)]
        public string PhoneNumber { get; set; } = null!;
    }
}
