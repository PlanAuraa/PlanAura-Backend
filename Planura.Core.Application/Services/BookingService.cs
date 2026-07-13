using AutoMapper;
using Microsoft.Extensions.Options;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class BookingService : IBookingService
{
    private const string DbUpdateExceptionName = "DbUpdateException";
    private const string ConcurrencyExceptionName = "DbUpdateConcurrencyException";
    private const string SqlExceptionName = "SqlException";
    private const int UniqueConstraintViolationNumber = 2601;
    private const int UniqueIndexViolationNumber = 2627;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly BookingOptions _bookingOptions;

    public BookingService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        INotificationService notificationService,
        IOptions<BookingOptions> bookingOptions)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _notificationService = notificationService;
        _bookingOptions = bookingOptions.Value;
    }

    public async Task<BookingRequestDto> CreateBookingRequestAsync(long clientUserId, CreateBookingRequestDto dto)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(dto.EventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), dto.EventPlanId);
        }

        var slotRepo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await slotRepo.GetWithSpecAsync(new VendorAvailabilityByIdSpecification(dto.AvailabilityId));
        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), dto.AvailabilityId);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            throw new SlotUnavailableExeption(
                $"Slot {dto.AvailabilityId} is no longer available for booking.");
        }

        var vendor = slot.Vendor;
        if (!VerificationStatus.IsApproved(vendor.VerificationStatus))
        {
            throw new BadRequestExeption("This vendor is not verified and cannot accept bookings.");
        }

        var vendorUser = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(vendor.UserId);
        if (vendorUser is null || !vendorUser.IsActive)
        {
            throw new BadRequestExeption("This vendor is not currently active.");
        }

        if (dto.VendorPackageId is not null)
        {
            var package = await _unitOfWork.Repository<VendorPackage, long>().GetAsync(dto.VendorPackageId.Value);
            if (package is null || package.VendorId != vendor.Id)
            {
                throw new NotFoundExeption(nameof(VendorPackage), dto.VendorPackageId.Value);
            }
        }

        var booking = new BookingRequest
        {
            EventPlanId = dto.EventPlanId,
            ClientId = clientId,
            VendorId = vendor.Id,
            VendorPackageId = dto.VendorPackageId,
            EventDate = DateOnly.FromDateTime(slot.StartAt.UtcDateTime),
            GuestCount = dto.GuestCount,
            AgreedPrice = dto.AgreedPrice,
            ClientMessage = dto.ClientMessage,
            Status = BookingStatus.Pending,
            PaymentStatus = BookingPaymentStatus.Unpaid
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Repository<BookingRequest, long>().AddAsync(booking);

            slot.Status = AvailabilityStatus.Held;
            slot.HoldExpiresAt = DateTimeOffset.UtcNow.AddHours(_bookingOptions.HoldTtlHours);
            slot.BookingRequest = booking;
            slotRepo.Update(slot);

            var history = new BookingStatusHistory
            {
                BookingRequest = booking,
                PreviousStatus = null,
                NewStatus = BookingStatus.Pending.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Booking request created by client."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex) when (IsSlotConflict(ex))
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw new SlotUnavailableExeption(
                $"Slot {dto.AvailabilityId} was just taken by another booking request.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
            vendor.UserId,
            NotificationTypes.BookingRequestReceived,
            "New booking request",
            $"You have a new booking request for {booking.EventDate:d}."));

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<BookingRequestDto> CancelBookingRequestAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BadRequestExeption(
                $"Cannot cancel a booking request with status '{booking.Status}'. Only pending requests can be cancelled.");
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            repo.Update(booking);

            var holdRepo = _unitOfWork.Repository<VendorAvailability, long>();
            var holds = await holdRepo.GetAllWithSpecAsync(
                new VendorAvailabilityByBookingRequestSpecification(bookingRequestId));
            holdRepo.DeleteRange(holds);

            var history = new BookingStatusHistory
            {
                BookingRequestId = bookingRequestId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = BookingStatus.Cancelled.ToString(),
                ChangedByUserId = clientUserId,
                Notes = "Cancelled by client."
            };
            await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(booking.VendorId);
        if (vendor is not null)
        {
            await NotifyBestEffortAsync(() => _notificationService.NotifyUserAsync(
                vendor.UserId,
                NotificationTypes.BookingCancelled,
                "Booking request cancelled",
                $"The client cancelled their booking request for {booking.EventDate:d}."));
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<BookingRequestDto> GetBookingRequestAsync(long bookingRequestId, long clientUserId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var booking = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        return _mapper.Map<BookingRequestDto>(booking);
    }

    public async Task<PagedResult<BookingRequestDto>> ListMyBookingRequestsAsync(long clientUserId, BookingRequestFilterDto filter)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;
        var skip = (page - 1) * pageSize;

        var repo = _unitOfWork.Repository<BookingRequest, long>();

        var totalCount = await repo.GetCountAsync(
            new BookingRequestsByClientSpecification(clientId, filter.Status, skip: null, take: null));

        var items = await repo.GetAllWithSpecAsync(
            new BookingRequestsByClientSpecification(clientId, filter.Status, skip, pageSize));

        return new PagedResult<BookingRequestDto>
        {
            Items = _mapper.Map<List<BookingRequestDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BookingRequestDto> FlagDisputeAsync(long bookingRequestId, long userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestExeption("A dispute reason is required.");
        }

        var clientId = await ResolveClientIdAsync(userId);

        var repo = _unitOfWork.Repository<BookingRequest, long>();
        var booking = await repo.GetAsync(bookingRequestId);
        if (booking is null || booking.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(BookingRequest), bookingRequestId);
        }

        if (booking.Status is not (BookingStatus.Accepted or BookingStatus.Completed))
        {
            throw new BadRequestExeption(
                $"Cannot raise a dispute on a booking request with status '{booking.Status}'.");
        }

        if (booking.DisputeStatus == DisputeStatus.Open)
        {
            throw new BadRequestExeption("A dispute is already open for this booking request.");
        }

        booking.DisputeStatus = DisputeStatus.Open;
        booking.DisputedAt = DateTimeOffset.UtcNow;
        booking.DisputedByUserId = userId;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        repo.Update(booking);

        var history = new BookingStatusHistory
        {
            BookingRequestId = bookingRequestId,
            PreviousStatus = booking.Status.ToString(),
            NewStatus = booking.Status.ToString(),
            ChangedByUserId = userId,
            Notes = $"Dispute raised: {reason}"
        };
        await _unitOfWork.Repository<BookingStatusHistory, long>().AddAsync(history);

        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<BookingRequestDto>(booking);
    }

    private async Task<long> ResolveClientIdAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return client.Id;
    }

    private static async Task NotifyBestEffortAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch
        {
            // Notifications are best-effort and must never fail the booking action itself.
        }
    }

    private static bool IsSlotConflict(Exception ex)
    {
        if (ex.GetType().Name == ConcurrencyExceptionName)
        {
            return true;
        }

        if (ex.GetType().Name != DbUpdateExceptionName || ex.InnerException is null)
        {
            return false;
        }

        var innerException = ex.InnerException;
        if (innerException.GetType().Name != SqlExceptionName)
        {
            return false;
        }

        var number = innerException.GetType().GetProperty("Number")?.GetValue(innerException) as int?;
        return number is UniqueConstraintViolationNumber or UniqueIndexViolationNumber;
    }
}
