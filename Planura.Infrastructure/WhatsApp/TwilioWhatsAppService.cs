using Microsoft.Extensions.Options;
using Planura.Core.Application.Abstraction.WhatsApp;
using Planura.Core.Application.Common;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Planura.Infrastructure.WhatsApp
{
    public class TwilioWhatsAppService : IWhatsAppService
    {
        private const string WhatsAppPrefix = "whatsapp:";
        private const string EgyptCountryCode = "+20";

        private readonly TwilioOptions _options;

        public TwilioWhatsAppService(IOptions<TwilioOptions> options)
        {
            _options = options.Value;
            TwilioClient.Init(_options.AccountSid, _options.AuthToken);
        }

        public async Task SendMessageAsync(string toPhoneNumber, string message)
        {
            var from = new PhoneNumber(WithWhatsAppPrefix(_options.WhatsAppFromNumber));
            var to = new PhoneNumber(WithWhatsAppPrefix(ToE164(toPhoneNumber)));

            await MessageResource.CreateAsync(to: to, from: from, body: message);
        }

        /// <summary>
        /// Normalizes a phone number stored as either local Egyptian format ("010xxxxxxxx")
        /// or already-E.164 ("+2010xxxxxxxx") into the E.164 form Twilio requires. Storage
        /// format is untouched elsewhere in the app (AuthService/registration/DB) — this
        /// conversion happens only on the outbound WhatsApp send path.
        /// </summary>
        private static string ToE164(string phoneNumber)
        {
            var trimmed = phoneNumber.Trim();

            if (trimmed.StartsWith(EgyptCountryCode, StringComparison.Ordinal))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("01", StringComparison.Ordinal))
            {
                return EgyptCountryCode + trimmed[1..];
            }

            return trimmed;
        }

        private static string WithWhatsAppPrefix(string phoneNumber)
            => phoneNumber.StartsWith(WhatsAppPrefix, StringComparison.OrdinalIgnoreCase)
                ? phoneNumber
                : WhatsAppPrefix + phoneNumber;
    }
}
