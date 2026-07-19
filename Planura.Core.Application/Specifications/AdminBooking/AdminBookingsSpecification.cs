using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Planura.Core.Application.Specifications.AdminBooking
{
    public class AdminBookingsSpecification :
        BaseSpecification<BookingRequest>
    {
        public AdminBookingsSpecification(AdminBookingFilterDto filter, bool applyPaging = true) : base(
          b =>
          (filter.Status == null || b.Status == filter.Status )
          &&
          (filter.PaymentStatus == null || b.PaymentStatus == filter.PaymentStatus)
          &&
          (filter.DisputeStatus == null || b.DisputeStatus == filter.DisputeStatus)
          &&
          (filter.FromDate == null || b.EventDate >= filter.FromDate)
          &&
          (filter.ToDate == null || b.EventDate <= filter.ToDate)
          &&
          (
    string.IsNullOrWhiteSpace(filter.Search)
    ||
   (
    b.Client.User.FullName != null &&
    b.Client.User.FullName.Contains(filter.Search)
)
||
(
    b.Vendor.BusinessName != null &&
    b.Vendor.BusinessName.Contains(filter.Search)
)
)
        )
        {
            AddInclude(b => b.Client);
            AddInclude(b => b.Vendor);
            AddInclude(b => b.VendorPackage);
            ApplyOrderByDescending(b => b.CreatedAt);
            if (applyPaging)
            {
                var skip = (filter.Page - 1) * filter.PageSize;
                ApplyPaging(skip, filter.PageSize);
            }

        }
    }
}
