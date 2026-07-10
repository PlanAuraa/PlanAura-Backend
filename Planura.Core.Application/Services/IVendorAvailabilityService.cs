using Planura.Core.Application.Models;

namespace Planura.Core.Application.Services;

public interface IVendorAvailabilityService
{
    Task<IEnumerable<VendorAvailabilityDto>> GetAllAsync();
    Task<IEnumerable<VendorAvailabilityDto>> GetByVendorAsync(long vendorId);
    Task<VendorAvailabilityDto> GetByIdAsync(long id);
    Task<AvailabilityCheckResultDto> CheckAvailabilityAsync(AvailabilityCheckDto dto);

    Task<VendorAvailabilityDto> CreateAsync(CreateVendorAvailabilityDto dto);
    Task<VendorAvailabilityDto> UpdateAsync(long id, UpdateVendorAvailabilityDto dto);
    Task DeleteAsync(long id);

    Task<VendorAvailabilityDto> BookSlotAsync(BookSlotDto dto);
    Task<VendorAvailabilityDto> CancelBookingAsync(long availabilityId);
}
