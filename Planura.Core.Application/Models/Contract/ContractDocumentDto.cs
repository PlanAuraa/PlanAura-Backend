namespace Planura.Core.Application.Models;

public class ContractDocumentDto
{
    public string ContractId { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public string FileName { get; set; } = "Contract.pdf";
    public string ContentType { get; set; } = "application/pdf";
}
