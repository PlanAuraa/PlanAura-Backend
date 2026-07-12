namespace Planura.Core.Application.Models.Vendor
{
    public class PortfolioLinkDto
    {
        public long Id { get; set; }

        public string Platform { get; set; } = null!;

        public string Url { get; set; } = null!;

        public string? Title { get; set; }
    }
}
