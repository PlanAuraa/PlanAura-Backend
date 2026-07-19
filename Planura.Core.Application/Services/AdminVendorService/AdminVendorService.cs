using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminVendor;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.AdminVendor
{
    /// <summary>
    /// Backs the "All Vendors" admin page (AdminDashboardPlan.md 2.4) - the roster of every vendor
    /// regardless of verification status, which the pending-only queue (2.2) and the public,
    /// Verified/Trusted-only browse endpoint (VendorController.BrowseVendors) both intentionally omit.
    /// Vendor detail and trust-promotion for this page are served by the existing
    /// IVendorVerificationService (GetVendorDetailsAsync / PromoteToTrustedAsync) rather than
    /// duplicated here.
    /// </summary>
    public class AdminVendorService : IAdminVendorService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminVendorService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<AdminVendorListItemDto>> ListVendorsAsync(AdminVendorFilterDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            var queryable = _unitOfWork.Repository<Vendor, long>().GetQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim().ToLowerInvariant();
                queryable = queryable.Where(v => v.VerificationStatus == status);
            }

            if (filter.CategoryId is not null)
            {
                queryable = queryable.Where(v => v.CategoryId == filter.CategoryId);
            }

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                var city = filter.City.Trim().ToLower();
                queryable = queryable.Where(v => v.City != null && v.City.ToLower().Contains(city));
            }

            if (filter.IsAccountActive is not null)
            {
                queryable = queryable.Where(v => v.User.IsActive == filter.IsAccountActive);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim().ToLower();
                queryable = queryable.Where(v =>
                    v.BusinessName.ToLower().Contains(search) ||
                    v.User.FullName.ToLower().Contains(search) ||
                    (v.User.Email != null && v.User.Email.ToLower().Contains(search)));
            }

            var totalCount = queryable.Count();

            var items = queryable
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new AdminVendorListItemDto
                {
                    VendorId = v.Id,
                    UserId = v.UserId,
                    VendorName = v.User.FullName,
                    BusinessName = v.BusinessName,
                    VendorType = v.VendorType,
                    CategoryName = v.Category != null ? v.Category.NameEn : null,
                    City = v.City,
                    VerificationStatus = v.VerificationStatus,
                    IsAccountActive = v.User.IsActive,
                    AvgRating = v.AvgRating,
                    TotalReviews = v.TotalReviews,
                    TotalCompletedBookings = v.TotalCompletedBookings,
                    CreatedAt = v.CreatedAt
                })
                .ToList();

            return Task.FromResult(new PagedResult<AdminVendorListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        public Task<AdminVendorStatusCountsDto> GetStatusCountsAsync()
        {
            var queryable = _unitOfWork.Repository<Vendor, long>().GetQueryable();

            var counts = new AdminVendorStatusCountsDto
            {
                Unverified = queryable.Count(v => v.VerificationStatus == VerificationStatus.Unverified),
                Pending = queryable.Count(v => v.VerificationStatus == VerificationStatus.Pending),
                Verified = queryable.Count(v => v.VerificationStatus == VerificationStatus.Verified),
                Trusted = queryable.Count(v => v.VerificationStatus == VerificationStatus.Trusted),
                Rejected = queryable.Count(v => v.VerificationStatus == VerificationStatus.Rejected)
            };

            return Task.FromResult(counts);
        }
    }
}
