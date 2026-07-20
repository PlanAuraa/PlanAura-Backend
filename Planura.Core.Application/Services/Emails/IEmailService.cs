using Planura.Core.Application.Models.Emails;

namespace Planura.Core.Application.Services.Emails
{
    public interface IEmailService
    {
        public Task SendEmail(Email email);
    }
}
