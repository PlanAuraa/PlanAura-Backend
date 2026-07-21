namespace Planura.Core.Application.Abstraction.Contract
{
    /// <summary>
    /// Renders a <see cref="ContractPdfModel"/> into a premium, branded PDF document.
    /// This service is only responsible for PDF rendering - it never talks to Gemini.
    /// </summary>
    public interface IPdfService
    {
        byte[] GenerateContractPdf(ContractPdfModel model);
    }
}
