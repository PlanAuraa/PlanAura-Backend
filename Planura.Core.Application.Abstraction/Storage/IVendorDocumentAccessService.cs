namespace Planura.Core.Application.Abstraction.Storage
{
    public interface IVendorDocumentAccessService
    {
        Task<VendorDocumentStreamResult> GetDocumentStreamAsync(Guid documentId, Guid currentUserId, bool isAdmin, CancellationToken ct = default);
    }
}
