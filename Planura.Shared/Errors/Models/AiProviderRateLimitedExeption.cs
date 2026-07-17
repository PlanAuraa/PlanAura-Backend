namespace Planura.Shared.Errors.Models
{
    public class AiProviderRateLimitedExeption : ApplicationException
    {
        public AiProviderRateLimitedExeption(string message) : base(message)
        {

        }
    }
}
