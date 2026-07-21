using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services.Contract;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;

    public ContractsController(IContractService contractService)
    {
        _contractService = contractService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateContractDto dto, CancellationToken cancellationToken)
    {
        var document = await _contractService.GenerateBookingContractAsync(dto, cancellationToken);
        Response.Headers["X-Contract-Id"] = document.ContractId;

        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpPost("vendor-partnership/generate")]
    public async Task<IActionResult> GenerateVendorPartnership([FromBody] GenerateVendorPartnershipDto dto, CancellationToken cancellationToken)
    {
        var document = await _contractService.GenerateVendorPartnershipContractAsync(dto, cancellationToken);
        Response.Headers["X-Contract-Id"] = document.ContractId;

        return File(document.Content, document.ContentType, document.FileName);
    }
}
