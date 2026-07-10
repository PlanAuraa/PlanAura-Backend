using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Planura.Core.Application.Abstraction.Notifications;

namespace Planura.Infrastructure.Notifications
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
        {
            var host = _configuration["Email:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                // Mirrors IdentityDataSeeder's AdminSeed pattern: an unconfigured optional
                // external integration is skipped (with a warning) rather than failing the request.
                _logger.LogWarning("Email:Host is not configured; skipping email to {ToEmail} ('{Subject}').", toEmail, subject);
                return;
            }

            var port = int.TryParse(_configuration["Email:Port"], out var parsedPort) ? parsedPort : 587;
            var user = _configuration["Email:User"];
            var password = _configuration["Email:Password"];
            var from = _configuration["Email:From"] ?? user ?? "no-reply@planura.dev";
            var enableSsl = !bool.TryParse(_configuration["Email:EnableSsl"], out var parsedSsl) || parsedSsl;

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

            if (!string.IsNullOrWhiteSpace(user))
            {
                await client.AuthenticateAsync(user, password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
