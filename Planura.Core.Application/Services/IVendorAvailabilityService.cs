using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IVendorAvailabilityService
{
    Task<IEnumerable<VendorAvailabilityDto>> GetAllAsync();
    Task<IEnumerable<VendorAvailabilityDto>> GetByVendorAsync(long vendorId);
    Task<VendorAvailabilityDto> GetByIdAsync(long id);
    Task<AvailabilityCheckResultDto> CheckAvailabilityAsync(AvailabilityCheckDto dto);

    Task<VendorAvailabilityDto> CreateAsync(long vendorId, CreateVendorAvailabilityDto dto);
    Task<VendorAvailabilityDto> UpdateAsync(long id, long vendorId, UpdateVendorAvailabilityDto dto);
    Task DeleteAsync(long id, long vendorId);

    Task<VendorAvailabilityDto> BookSlotAsync(BookSlotDto dto);
    Task<VendorAvailabilityDto> CancelBookingAsync(long availabilityId);
}
