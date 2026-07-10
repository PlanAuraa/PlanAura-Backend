namespace Planura.Core.Application.Abstraction.Vendors.Contracts
{
    public class VerificationHistoryDto
    {
        public Guid VendorProfileId { get; set; }
        public string BusinessName { get; set; } = null!;
        public List<VerificationRequestDto> Requests { get; set; } = new();
    }
}
