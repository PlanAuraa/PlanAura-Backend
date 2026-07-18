namespace Planura.Shared.Errors.Models
{
    public class AiProviderUnavailableExeption : ApplicationException
    {
        public AiProviderUnavailableExeption(string message) : base(message)
        {

        }
    }
}
