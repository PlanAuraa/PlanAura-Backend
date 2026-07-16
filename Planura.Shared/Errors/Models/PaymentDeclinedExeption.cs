namespace Planura.Shared.Errors.Models
{
    public class PaymentDeclinedExeption : ApplicationException
    {
        public PaymentDeclinedExeption(string message) : base(message)
        {

        }
    }
}
