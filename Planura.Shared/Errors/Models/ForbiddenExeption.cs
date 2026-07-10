namespace Planura.Shared.Errors.Models
{
    public class ForbiddenExeption : ApplicationException
    {
        public ForbiddenExeption(string message) : base(message)
        {

        }
    }
}
