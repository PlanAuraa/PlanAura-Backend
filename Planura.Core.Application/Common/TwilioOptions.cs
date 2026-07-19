namespace Planura.Core.Application.Common;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string WhatsAppFromNumber { get; set; } = string.Empty;
}
