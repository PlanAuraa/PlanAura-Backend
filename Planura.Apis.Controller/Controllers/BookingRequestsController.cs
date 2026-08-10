using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.Booking;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/booking-requests")]
[Authorize(Policy = AuthorizationPolicies.ClientOnly)]
public class BookingRequestsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICurrentUserService _currentUserService;

    public BookingRequestsController(IBookingService bookingService, ICurrentUserService currentUserService)
    {
        _bookingService = bookingService;
        _currentUserService = currentUserService;
    }

    private long CurrentUserId => _currentUserService.UserId
        ?? throw new UnAuthorizedExeption("No authenticated user.");

    /// <summary>
    /// Prices the booking the client has configured but not yet submitted: total, amount due now, and
    /// remaining balance. Cheap and AI-free (unlike <see cref="PreviewAgreement"/>), so checkout can
    /// show the real figures as soon as a slot is picked rather than waiting for a contract to be drafted.
    /// </summary>
    [HttpPost("payment-quote")]
    public async Task<ActionResult<BookingPaymentQuoteDto>> GetPaymentQuote([FromBody] BookingPaymentQuoteRequestDto dto)
    {
        var result = await _bookingService.GetBookingPaymentQuoteAsync(CurrentUserId, dto);
        return Ok(result);
    }

    /// <summary>
    /// Generates the Booking Agreement for the current (fixed) payment-step details so the client can
    /// review it before confirming. Returns a token that <see cref="Create"/> redeems to bind that
    /// exact contract to the new booking.
    /// </summary>
    [HttpPost("agreement-preview")]
    public async Task<ActionResult<AgreementPreviewResultDto>> PreviewAgreement([FromBody] AgreementPreviewRequestDto dto)
    {
        var result = await _bookingService.PreviewBookingAgreementAsync(CurrentUserId, dto);
        return Ok(result);
    }

    /// <summary>The full-vs-deposit payment breakdown for the chosen slot + package, so the client sees the
    /// deposit / remainder / total before paying. Read-only — creates nothing.</summary>
    [HttpPost("payment-preview")]
    public async Task<ActionResult<PaymentPreviewDto>> PreviewPayment([FromBody] AgreementPreviewRequestDto dto)
    {
        var result = await _bookingService.PreviewPaymentAsync(CurrentUserId, dto);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BookingRequestDto>> Create([FromBody] CreateBookingRequestDto dto)
    {
        var result = await _bookingService.CreateBookingRequestAsync(CurrentUserId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BookingRequestDto>>> List([FromQuery] BookingRequestFilterDto filter)
    {
        var result = await _bookingService.ListMyBookingRequestsAsync(CurrentUserId, filter);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BookingRequestDto>> GetById(long id)
    {
        var result = await _bookingService.GetBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>
    /// Client pays the outstanding remainder on their deposit booking on-session. If the response has
    /// RequiresAction=true, the frontend completes authentication (SCA) using ClientSecret; otherwise the
    /// booking is already fully paid.
    /// </summary>
    [HttpPost("{id:long}/pay-remainder")]
    public async Task<ActionResult<PayRemainderResultDto>> PayRemainder(long id)
    {
        var result = await _bookingService.PayRemainderAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>The permanent "Booking Activity" audit trail — the source of truth for what
    /// happened to this booking, independent of any (best-effort) notification.</summary>
    [HttpGet("{id:long}/timeline")]
    public async Task<ActionResult<List<BookingStatusHistoryEntryDto>>> GetTimeline(long id)
    {
        var result = await _bookingService.GetBookingTimelineAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPatch("{id:long}/cancel")]
    public async Task<ActionResult<BookingRequestDto>> Cancel(long id)
    {
        var result = await _bookingService.CancelBookingRequestAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>Estimated refund if the client requested cancellation right now — shown before they commit.</summary>
    [HttpGet("{id:long}/cancellation-quote")]
    public async Task<ActionResult<CancellationQuoteDto>> GetCancellationQuote(long id)
    {
        var result = await _bookingService.GetCancellationQuoteAsync(id, CurrentUserId);
        return Ok(result);
    }

    /// <summary>
    /// Requests cancellation of an Accepted booking. Does not cancel it immediately — moves it to
    /// CancellationRequested pending admin approval (see api/admin/bookings/cancellation-requests).
    /// </summary>
    [HttpPost("{id:long}/request-cancellation")]
    public async Task<ActionResult<BookingRequestDto>> RequestCancellation(long id, [FromBody] RequestCancellationDto dto)
    {
        var result = await _bookingService.RequestCancellationAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }

    /// <summary>
    /// The client confirms the service was delivered for a booking sitting in
    /// AwaitingConfirmation, completing it. "Report a problem" instead uses POST .../dispute.
    /// </summary>
    [HttpPost("{id:long}/confirm-completion")]
    public async Task<ActionResult<BookingRequestDto>> ConfirmCompletion(long id)
    {
        var result = await _bookingService.ConfirmServiceDeliveredAsync(id, CurrentUserId);
        return Ok(result);
    }

    [HttpPost("{id:long}/dispute")]
    public async Task<ActionResult<BookingRequestDto>> Dispute(long id, [FromBody] FlagDisputeDto dto)
    {
        var result = await _bookingService.FlagDisputeAsync(id, CurrentUserId, dto.Reason);
        return Ok(result);
    }

    /// <summary>Sends a message on this booking's client/vendor chat thread. Unlocked once the vendor
    /// has accepted the request.</summary>
    [HttpPost("{id:long}/messages")]
    public async Task<ActionResult<BookingChatMessageDto>> SendMessage(long id, [FromBody] SendBookingChatMessageDto dto)
    {
        var result = await _bookingService.SendChatMessageAsync(id, CurrentUserId, dto);
        return Ok(result);
    }

    /// <summary>Lists this booking's chat messages, oldest first. Pass afterId to poll for only new ones.</summary>
    [HttpGet("{id:long}/messages")]
    public async Task<ActionResult<IEnumerable<BookingChatMessageDto>>> GetMessages(long id, [FromQuery] long? afterId)
    {
        var result = await _bookingService.GetChatMessagesAsync(id, CurrentUserId, afterId);
        return Ok(result);
    }
}
