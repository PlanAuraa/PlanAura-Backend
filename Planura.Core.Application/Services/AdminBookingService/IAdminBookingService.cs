using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Services.AdminBooking
{
    public interface IAdminBookingService
    {
        Task<IEnumerable<AdminDisputeListItemDto>> GetOpenDisputesAsync();
        Task<AdminDisputeDetailsDto> GetDisputeDetailsAsync(long bookingId);
        Task ResolveDisputeAsync(long bookingId , long adminId , ResolveDisputeDto dto);
        Task<PagedResult<AdminBookingDto>> GetBookingsAsync(AdminBookingFilterDto filter);
    }
}
