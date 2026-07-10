using Planura.Core.Application.Abstraction.Vendors.Contracts;
using Planura.Core.Domain.Enums;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Vendors
{
    // Document requirement rule depends on VendorBusinessType, never on VendorCategory (see plan
    // amendment). Shared by registration and resubmission since both submit a fresh document set.
    public static class VendorDocumentRequirements
    {
        private static readonly IReadOnlyDictionary<VendorBusinessType, DocumentType[]> RequiredByBusinessType =
            new Dictionary<VendorBusinessType, DocumentType[]>
            {
                [VendorBusinessType.Individual] = new[] { DocumentType.NationalId },
                [VendorBusinessType.Business] = new[] { DocumentType.NationalId, DocumentType.CommercialRegistration, DocumentType.TaxCard }
            };

        public static void Validate(VendorBusinessType businessType, IReadOnlyCollection<VendorDocumentUpload> documents)
        {
            var providedTypes = documents.Select(d => d.DocumentType).ToHashSet();
            var required = RequiredByBusinessType[businessType];
            var missing = required.Where(r => !providedTypes.Contains(r)).ToList();

            if (missing.Count > 0)
            {
                throw new ValidationExeption("Required verification documents are missing.")
                {
                    Errors = missing.Select(m => $"{m} document is required for {businessType} vendors.")
                };
            }
        }
    }
}
