using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Application.Models.AdminPayment;
using Planura.Core.Application.Services.AdminPayment;
using Planura.Core.Application.Specifications;
using Planura.Core.Application.Specifications.AdminBooking;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AdminBooking
{
    public class AdminBookingService : IAdminBookingService
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAdminPaymentService _adminPaymentService;
        private readonly INotificationService _notificationService;

        public AdminBookingService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IAdminPaymentService adminPaymentService,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _adminPaymentService = adminPaymentService;
            _notificationService = notificationService;
        }
        public async Task<IEnumerable<AdminDisputeListItemDto>> GetOpenDisputesAsync()
{
            var spec = new OpenDisputesSpecification();
            var disputes = await _unitOfWork
                .Repository<BookingRequest,long>()
                .GetAllWithSpecAsync(spec);

            return _mapper.Map<IEnumerable<AdminDisputeListItemDto>>(disputes);
        }
        public async Task<AdminDisputeDetailsDto> GetDisputeDetailsAsync(long bookingId)
        {
            if (bookingId <= 0)
            {
                throw new BadRequestExeption("Booking id must be greater than zero.");
            }
            
                var spec = new DisputeDetailsSpecification(bookingId);
                var dispute = await _unitOfWork
                    .Repository<BookingRequest, long>()
                    .GetWithSpecAsync(spec);
                if(dispute == null)
                {
                throw new NotFoundExeption("Dispute", bookingId);
            }
                
                    return _mapper.Map<AdminDisputeDetailsDto>(dispute);
            
        }


        public async Task ResolveDisputeAsync(long bookingId, long adminId, ResolveDisputeDto dto)
        {
            var req = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingId);
            if (req == null) throw new NotFoundExeption(nameof(BookingRequest), bookingId);
            if ( req.DisputeStatus != DisputeStatus.Open)
            {
                throw new BadRequestExeption("Only open disputes can be resolved.");
            }

            // Execute the refund (if requested) before touching booking/history state, mirroring the
            // cancellation-approval path — this was previously accepted by the DTO/admin UI but never
            // actually acted on, silently leaving RefundClient a no-op.
            var refunded = false;
            if (dto.RefundClient)
            {
                var payment = await _unitOfWork.Repository<Payment, long>()
                    .GetWithSpecAsync(new CompletedPaymentByBookingRequestSpecification(bookingId));
                if (payment is null)
                {
                    throw new BadRequestExeption(
                        "No captured payment was found for this booking; it cannot be refunded as part of this resolution.");
                }

                await _adminPaymentService.RefundPaymentAsync(payment.Id, adminId, new RefundPaymentDto
                {
                    Reason = $"Dispute resolution refund: {dto.ResolutionNotes}"
                });
                refunded = true;
            }

            var now = DateTimeOffset.UtcNow;
            req.DisputeStatus = DisputeStatus.Resolved;
            req.ResolutionNotes = dto.ResolutionNotes;
            req.ResolvedByAdminId = adminId;
            req.ResolvedAt = now;
            if (refunded)
            {
                req.RefundStatus = RefundStatus.Processed;
            }
            req.UpdatedAt = now;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                _unitOfWork.Repository<BookingRequest, long>().Update(req);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = bookingId,
                    PreviousStatus = req.Status.ToString(),
                    NewStatus = req.Status.ToString(),
                    ChangedByUserId = adminId,
                    Notes = $"{BookingStatusHistoryNotes.DisputeResolvedPrefix}{dto.ResolutionNotes}" +
                        (refunded ? " (client refunded)" : "")
                };
                await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            await NotifyBestEffortAsync(async () =>
            {
                var client = await _unitOfWork.Repository<Client, long>().GetAsync(req.ClientId);
                if (client is not null)
                {
                    await _notificationService.NotifyUserAsync(
                        client.UserId,
                        NotificationTypes.DisputeResolved,
                        "Dispute resolved",
                        $"Your dispute for the booking on {req.EventDate:d} has been resolved: {dto.ResolutionNotes}" +
                        (refunded ? " A refund has been processed." : ""));
                }
            });
            await NotifyBestEffortAsync(async () =>
            {
                var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(req.VendorId);
                if (vendor is not null)
                {
                    await _notificationService.NotifyUserAsync(
                        vendor.UserId,
                        NotificationTypes.DisputeResolved,
                        "Dispute resolved",
                        $"The dispute for the booking on {req.EventDate:d} has been resolved: {dto.ResolutionNotes}" +
                        (refunded ? " The client has been refunded." : ""));
                }
            });
        }


        public async Task<PagedResult<AdminBookingDto>> GetBookingsAsync(AdminBookingFilterDto filter)
        {
            var specification = new AdminBookingsSpecification(filter);
            var listSpec = new AdminBookingsSpecification(filter);

            var countSpec = new AdminBookingsSpecification(filter, false);
            var bookings = await _unitOfWork
                .Repository<BookingRequest, long>()
                .GetAllWithSpecAsync(listSpec);

            var totalCount = await _unitOfWork
                .Repository<BookingRequest, long>()
                .GetCountAsync(countSpec);

            var bookingDtos = _mapper.Map<List<AdminBookingDto>>(bookings);

            return new PagedResult<AdminBookingDto>
            {
                Items = bookingDtos,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<IEnumerable<CancellationRequestListItemDto>> GetCancellationRequestsAsync()
        {
            var spec = new CancellationRequestsSpecification();
            var bookings = await _unitOfWork.Repository<BookingRequest, long>().GetAllWithSpecAsync(spec);

            return bookings.Select(b => new CancellationRequestListItemDto
            {
                BookingId = b.Id,
                ClientId = b.ClientId,
                ClientName = b.Client?.User?.FullName,
                ClientEmail = b.Client?.User?.Email,
                ClientPhone = b.Client?.User?.PhoneNumber,
                VendorId = b.VendorId,
                VendorName = b.Vendor?.BusinessName,
                EventDate = b.EventDate,
                AgreedPrice = b.AgreedPrice,
                CancellationReason = b.CancellationReason,
                CancellationRequestedAt = b.CancellationRequestedAt,
                CancellationRefundPercent = b.CancellationRefundPercent,
                CancellationRefundAmount = b.CancellationRefundAmount
            });
        }

        public async Task<AdminBookingDto> ApproveCancellationAsync(long bookingId, long adminId, ApproveCancellationDto dto)
        {
            var repo = _unitOfWork.Repository<BookingRequest, long>();
            var booking = await repo.GetAsync(bookingId);
            if (booking is null)
            {
                throw new NotFoundExeption(nameof(BookingRequest), bookingId);
            }

            if (booking.Status != BookingStatus.CancellationRequested)
            {
                throw new BadRequestExeption(
                    $"Cannot approve a cancellation for a booking request with status '{booking.Status}'. " +
                    "Only bookings with a pending cancellation request can be approved.");
            }

            // Execute the refund through the existing admin-payment path (real Stripe refund, partial
            // or full) BEFORE touching booking/slot state, mirroring how Stripe calls elsewhere in this
            // app (authorize/capture) happen before their DB transaction. Falls back to the estimate
            // locked in at request time when the admin doesn't override the amount.
            var refundAmount = dto.Amount ?? booking.CancellationRefundAmount ?? 0m;

            if (refundAmount > 0)
            {
                // Refundable = Completed (full-payment) or FullyPaid (deposit whose remainder was collected).
                // A deposit-only booking has no refundable payment here and is non-refundable by policy.
                var payment = await _unitOfWork.Repository<Payment, long>()
                    .GetWithSpecAsync(new RefundablePaymentByBookingRequestSpecification(bookingId));
                if (payment is null)
                {
                    throw new BadRequestExeption(
                        "No refundable (fully-captured) payment was found for this booking; the cancellation cannot be approved.");
                }

                await _adminPaymentService.RefundPaymentAsync(payment.Id, adminId, new RefundPaymentDto
                {
                    Reason = string.IsNullOrWhiteSpace(dto.Note)
                        ? "Cancellation approved by admin."
                        : dto.Note,
                    Amount = refundAmount
                });
                // RefundPaymentAsync already set Payment.Status/RefundedAt and BookingRequest.PaymentStatus
                // on this same tracked `booking` instance (same DbContext, so EF's identity map resolves it
                // to the object we already hold) and saved them - only the fields below still need setting.
            }
            // A zero-amount refund (e.g. the policy's 0% tier) is skipped entirely — Stripe rejects
            // refund calls with amount <= 0 outright, and there is nothing to refund. The booking still
            // gets cancelled and the slot released; PaymentStatus simply stays Paid since the vendor
            // keeps the full amount.

            var now = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = now;
            booking.CancellationReviewNotes = dto.Note;
            booking.CancellationReviewedAt = now;
            booking.RefundStatus = RefundStatus.Processed;
            booking.UpdatedAt = now;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                repo.Update(booking);

                var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
                var holds = await holdRepo.GetAllWithSpecAsync(
                    new VendorAvailabilityByBookingRequestSpecification(bookingId));
                foreach (var hold in holds)
                {
                    hold.Status = AvailabilityStatus.Available;
                    hold.BookingRequestId = null;
                    hold.BookingRequest = null;
                    hold.HoldExpiresAt = null;
                    holdRepo.Update(hold);
                }

                var history = new BookingStatusHistory
                {
                    BookingRequestId = bookingId,
                    PreviousStatus = BookingStatus.CancellationRequested.ToString(),
                    NewStatus = BookingStatus.Cancelled.ToString(),
                    ChangedByUserId = adminId,
                    Notes = $"Cancellation approved by admin (refunded {refundAmount:0.00}).{(string.IsNullOrWhiteSpace(dto.Note) ? "" : $" {dto.Note}")}"
                };
                await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            await NotifyBestEffortAsync(async () =>
            {
                var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
                if (client is not null)
                {
                    var refundMessage = refundAmount > 0
                        ? $"A refund of {refundAmount:0.00} has been processed."
                        : "Per our cancellation policy, no refund applies for this cancellation.";
                    await _notificationService.NotifyUserAsync(
                        client.UserId,
                        NotificationTypes.BookingCancellationApproved,
                        "Cancellation approved",
                        $"Your cancellation for the booking on {booking.EventDate:d} was approved. {refundMessage}");
                }
            });
            await NotifyBestEffortAsync(async () =>
            {
                var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
                if (vendor is not null)
                {
                    await _notificationService.NotifyUserAsync(
                        vendor.UserId,
                        NotificationTypes.BookingCancellationApproved,
                        "Booking cancelled",
                        $"The booking for {booking.EventDate:d} was cancelled and the slot has been released.");
                }
            });

            return _mapper.Map<AdminBookingDto>(booking);
        }

        public async Task<AdminBookingDto> RejectCancellationAsync(long bookingId, long adminId, RejectCancellationDto dto)
        {
            var repo = _unitOfWork.Repository<BookingRequest, long>();
            var booking = await repo.GetAsync(bookingId);
            if (booking is null)
            {
                throw new NotFoundExeption(nameof(BookingRequest), bookingId);
            }

            if (booking.Status != BookingStatus.CancellationRequested)
            {
                throw new BadRequestExeption(
                    $"Cannot reject a cancellation for a booking request with status '{booking.Status}'. " +
                    "Only bookings with a pending cancellation request can be rejected.");
            }

            var now = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.Accepted;
            booking.CancellationReviewNotes = dto.Note;
            booking.CancellationRefundPercent = null;
            booking.CancellationRefundAmount = null;
            booking.RefundStatus = RefundStatus.Rejected;
            booking.UpdatedAt = now;

            // Deliberately no slot/payment changes - the vendor's slot was never released and no
            // refund was ever created while the request was pending, so there's nothing to undo.
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                repo.Update(booking);

                var history = new BookingStatusHistory
                {
                    BookingRequestId = bookingId,
                    PreviousStatus = BookingStatus.CancellationRequested.ToString(),
                    NewStatus = BookingStatus.Accepted.ToString(),
                    ChangedByUserId = adminId,
                    Notes = $"Cancellation rejected by admin: {dto.Note}"
                };
                await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            await NotifyBestEffortAsync(async () =>
            {
                var client = await _unitOfWork.Repository<Client, long>().GetAsync(booking.ClientId);
                if (client is not null)
                {
                    await _notificationService.NotifyUserAsync(
                        client.UserId,
                        NotificationTypes.BookingCancellationRejected,
                        "Cancellation request declined",
                        $"Your cancellation request for the booking on {booking.EventDate:d} was declined: {dto.Note}");
                }
            });

            return _mapper.Map<AdminBookingDto>(booking);
        }

        public async Task<AdminBookingPaymentDetailDto> GetBookingPaymentDetailAsync(long bookingId)
        {
            var booking = await _unitOfWork.Repository<BookingRequest, long>()
                .GetWithSpecAsync(new AdminBookingPaymentDetailSpecification(bookingId));
            if (booking is null)
            {
                throw new NotFoundExeption(nameof(BookingRequest), bookingId);
            }

            var payments = await _unitOfWork.Repository<Payment, long>()
                .GetAllWithSpecAsync(new PaymentsByBookingRequestSpecification(bookingId));

            var history = await _unitOfWork.Repository<BookingStatusHistory, long>()
                .GetAllWithSpecAsync(new BookingStatusHistoryByBookingRequestSpecification(bookingId));

            return new AdminBookingPaymentDetailDto
            {
                BookingId = booking.Id,
                Status = booking.Status,
                EventDate = booking.EventDate,

                ClientId = booking.ClientId,
                ClientName = booking.Client?.User?.FullName,
                ClientEmail = booking.Client?.User?.Email,
                ClientPhone = booking.Client?.User?.PhoneNumber,

                VendorId = booking.VendorId,
                VendorName = booking.Vendor?.BusinessName,

                TotalAmount = booking.AgreedPrice,
                PaymentStatus = booking.PaymentStatus,

                RefundStatus = booking.RefundStatus,
                CancellationReason = booking.CancellationReason,
                CancellationRequestedAt = booking.CancellationRequestedAt,
                CancellationReviewNotes = booking.CancellationReviewNotes,
                CancellationRefundPercent = booking.CancellationRefundPercent,
                CancellationRefundAmount = booking.CancellationRefundAmount,
                CancelledAt = booking.CancelledAt,

                DisputeStatus = booking.DisputeStatus,
                DisputedAt = booking.DisputedAt,
                ResolutionNotes = booking.ResolutionNotes,

                PaymentTimeline = payments.Select(p => new PaymentTimelineEntryDto
                {
                    PaymentId = p.Id,
                    Amount = p.Amount,
                    Status = p.Status,
                    PaymentMethod = p.PaymentMethod,
                    GatewayReference = p.GatewayReference,
                    AuthorizedAt = p.AuthorizedAt,
                    PaidAt = p.PaidAt,
                    CancelledAt = p.CancelledAt,
                    RefundedAt = p.RefundedAt,
                    RefundReason = p.RefundReason,
                    CreatedAt = p.CreatedAt
                }).ToList(),

                StatusHistory = history.Select(h => new BookingStatusHistoryEntryDto
                {
                    PreviousStatus = h.PreviousStatus,
                    NewStatus = h.NewStatus,
                    ChangedByUserId = h.ChangedByUserId,
                    ChangedByName = h.ChangedByUser?.FullName,
                    Notes = h.Notes,
                    ChangedAt = h.ChangedAt
                }).ToList()
            };
        }

        private static async Task NotifyBestEffortAsync(Func<Task> notify)
        {
            try
            {
                await notify();
            }
            catch
            {
                // Notifications are best-effort and must never fail the admin action itself.
            }
        }
    }
}
