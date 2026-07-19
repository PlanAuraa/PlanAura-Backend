namespace Planura.Core.Application.Abstraction.WhatsApp
{
    public interface IWhatsAppService
    {
        Task SendMessageAsync(string toPhoneNumber, string message);
    }
}
