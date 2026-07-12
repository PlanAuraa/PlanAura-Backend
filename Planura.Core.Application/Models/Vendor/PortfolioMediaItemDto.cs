namespace Planura.Core.Application.Models.Vendor
{
    public class PortfolioMediaItemDto
    {
        public long Id { get; set; }

        public string? FileUrl { get; set; }

        public string? Title { get; set; }

        public int DisplayOrder { get; set; }
    }
}
