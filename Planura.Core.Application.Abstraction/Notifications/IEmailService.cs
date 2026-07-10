namespace Planura.Core.Application.Abstraction.Notifications
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
    }
}
