namespace Planura.Shared.Errors.Models
{
    public class SlotUnavailableExeption : ApplicationException
    {
        public SlotUnavailableExeption(string message) : base(message)
        {

        }
    }
}
