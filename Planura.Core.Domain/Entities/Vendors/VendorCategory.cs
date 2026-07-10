namespace Planura.Core.Domain.Entities.Vendors
{
    public class VendorCategory
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;

        public ICollection<VendorProfile> VendorProfiles { get; set; } = new List<VendorProfile>();
    }
}
