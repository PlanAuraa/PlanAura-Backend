using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services;

public class VendorAvailabilityService : IVendorAvailabilityService
{
    private const string ConcurrencyExceptionName = "DbUpdateConcurrencyException";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public VendorAvailabilityService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<VendorAvailabilityDto>> GetAllAsync()
    {
        var slots = await _unitOfWork.Repository<VendorAvailability, long>().GetAllAsync();
        return _mapper.Map<IEnumerable<VendorAvailabilityDto>>(slots);
    }

    public async Task<IEnumerable<VendorAvailabilityDto>> GetByVendorAsync(long vendorId)
    {
        await EnsureVendorExistsAsync(vendorId);

        var slots = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetAllWithSpecAsync(new VendorAvailabilityByVendorSpecification(vendorId));

        return _mapper.Map<IEnumerable<VendorAvailabilityDto>>(slots);
    }

    public async Task<VendorAvailabilityDto> GetByIdAsync(long id)
    {
        var slot = await GetSlotOrThrowAsync(id, tracking: false);
        return _mapper.Map<VendorAvailabilityDto>(slot);
    }

    public async Task<AvailabilityCheckResultDto> CheckAvailabilityAsync(AvailabilityCheckDto dto)
    {
        await EnsureVendorExistsAsync(dto.VendorId);
        ValidateRange(dto.StartAt, dto.EndAt);

        var isAvailable = await IsVendorAvailableAsync(dto.VendorId, dto.StartAt, dto.EndAt);

        return new AvailabilityCheckResultDto
        {
            VendorId = dto.VendorId,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            IsAvailable = isAvailable,
            Message = isAvailable
                ? "Vendor is available for the requested time."
                : "Vendor is NOT available for the requested time (conflict with an existing booking/blocked slot)."
        };
    }

    public async Task<VendorAvailabilityDto> CreateAsync(CreateVendorAvailabilityDto dto)
    {
        await EnsureVendorExistsAsync(dto.VendorId);
        ValidateRange(dto.StartAt, dto.EndAt);

        await EnsureNoOverlapAsync(dto.VendorId, dto.StartAt, dto.EndAt, excludeId: null);

        var slot = new VendorAvailability
        {
            VendorId = dto.VendorId,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            Status = AvailabilityStatus.Available,
            BookingRequestId = null
        };

        await _unitOfWork.Repository<VendorAvailability, long>().AddAsync(slot);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<VendorAvailabilityDto>(slot);
    }

    public async Task<VendorAvailabilityDto> UpdateAsync(long id, UpdateVendorAvailabilityDto dto)
    {
        var repo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await repo.GetWithSpecAsync(new VendorAvailabilityByIdSpecification(id));

        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), id);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            throw new BadRequestExeption(
                $"Cannot edit a slot with status '{slot.Status}'. Only available slots can be edited.");
        }

        ValidateRange(dto.StartAt, dto.EndAt);

        await EnsureNoOverlapAsync(slot.VendorId, dto.StartAt, dto.EndAt, excludeId: id);

        slot.StartAt = dto.StartAt;
        slot.EndAt = dto.EndAt;

        repo.Update(slot);

        await SaveWithConcurrencyGuardAsync(
            "This availability slot was modified by another request. Please refresh and try again.");

        return _mapper.Map<VendorAvailabilityDto>(slot);
    }

    public async Task DeleteAsync(long id)
    {
        var repo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await repo.GetAsync(id);

        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), id);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            throw new BadRequestExeption(
                $"Cannot delete a slot with status '{slot.Status}'. Only available slots can be deleted.");
        }

        repo.Delete(slot);

        await SaveWithConcurrencyGuardAsync(
            "This availability slot was modified by another request. Please refresh and try again.");
    }

    public async Task<VendorAvailabilityDto> BookSlotAsync(BookSlotDto dto)
    {
        var repo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await repo.GetWithSpecAsync(new VendorAvailabilityByIdSpecification(dto.AvailabilityId));

        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), dto.AvailabilityId);
        }

        if (slot.Status != AvailabilityStatus.Available)
        {
            throw new BadRequestExeption(
                $"Slot {dto.AvailabilityId} is not available for booking. Current status: '{slot.Status}'.");
        }

        slot.Status = AvailabilityStatus.Booked;
        slot.BookingRequestId = dto.BookingRequestId;

        repo.Update(slot);

        await SaveWithConcurrencyGuardAsync(
            "This slot was just booked by another request. Double booking prevented.");

        return _mapper.Map<VendorAvailabilityDto>(slot);
    }

    public async Task<VendorAvailabilityDto> CancelBookingAsync(long availabilityId)
    {
        var repo = _unitOfWork.Repository<VendorAvailability, long>();
        var slot = await repo.GetWithSpecAsync(new VendorAvailabilityByIdSpecification(availabilityId));

        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), availabilityId);
        }

        if (slot.Status != AvailabilityStatus.Booked)
        {
            throw new BadRequestExeption(
                $"Cannot cancel booking on a slot with status '{slot.Status}'. Only booked slots can be cancelled.");
        }

        slot.Status = AvailabilityStatus.Available;
        slot.BookingRequestId = null;

        repo.Update(slot);

        await SaveWithConcurrencyGuardAsync(
            "This availability slot was modified by another request. Please refresh and try again.");

        return _mapper.Map<VendorAvailabilityDto>(slot);
    }

    private async Task SaveWithConcurrencyGuardAsync(string concurrencyErrorMessage)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex.GetType().Name == ConcurrencyExceptionName)
        {
            throw new BadRequestExeption(concurrencyErrorMessage);
        }
    }

    private async Task<bool> IsVendorAvailableAsync(long vendorId, DateTimeOffset startAt, DateTimeOffset endAt)
    {
        var conflicting = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetAllWithSpecAsync(new VendorBookedAvailabilityOverlapSpecification(vendorId, startAt, endAt));

        if (conflicting.Any(slot => slot.Status != AvailabilityStatus.Available))
        {
            return false;
        }

        var allSlots = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetAllWithSpecAsync(new VendorAvailabilityByVendorSpecification(vendorId));

        var hasAnyAvailableSlot = allSlots.Any(slot => slot.Status == AvailabilityStatus.Available);

        if (!hasAnyAvailableSlot)
        {
            return true;
        }

        return allSlots
            .Where(slot => slot.Status == AvailabilityStatus.Available)
            .Any(slot => slot.StartAt <= startAt && slot.EndAt >= endAt);
    }

    private async Task EnsureNoOverlapAsync(long vendorId, DateTimeOffset startAt, DateTimeOffset endAt, long? excludeId)
    {
        var overlapping = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetAllWithSpecAsync(new VendorBookedAvailabilityOverlapSpecification(vendorId, startAt, endAt));

        if (excludeId is not null)
        {
            overlapping = overlapping.Where(slot => slot.Id != excludeId.Value);
        }

        if (overlapping.Any())
        {
            throw new BadRequestExeption(
                "The requested time range overlaps with an existing availability slot for this vendor.");
        }
    }

    private async Task<VendorAvailability> GetSlotOrThrowAsync(long id, bool tracking)
    {
        var slot = await _unitOfWork.Repository<VendorAvailability, long>()
            .GetWithSpecAsync(new VendorAvailabilityByIdSpecification(id));

        if (slot is null)
        {
            throw new NotFoundExeption(nameof(VendorAvailability), id);
        }

        return slot;
    }

    private static void ValidateRange(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (endAt <= startAt)
        {
            throw new BadRequestExeption("EndAt must be later than StartAt.");
        }
    }

    private async Task EnsureVendorExistsAsync(long vendorId)
    {
        var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
        if (vendor is null)
        {
            throw new NotFoundExeption(nameof(Vendor), vendorId);
        }
    }
}
