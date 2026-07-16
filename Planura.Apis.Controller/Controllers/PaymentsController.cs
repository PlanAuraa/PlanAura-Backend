using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUserService;

    public PaymentsController(IPaymentService paymentService, ICurrentUserService currentUserService)
    {
        _paymentService = paymentService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [HttpGet("my-transactions")]
    public async Task<ActionResult<PagedResult<PaymentDto>>> MyTransactions([FromQuery] TransactionsFilterDto filter)
    {
        var result = await _paymentService.ListMyTransactionsAsync(CurrentUserId, filter);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        await _paymentService.HandleStripeWebhookAsync(rawJson, signature);

        return Ok();
    }
}
