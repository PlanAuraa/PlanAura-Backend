namespace Planura.Shared.Errors.Models
{
    public class AiProviderTimeoutExeption : ApplicationException
    {
        public AiProviderTimeoutExeption(string message) : base(message)
        {

        }
    }
}
