using Planura.Core.Application.Models.AdminReport;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Services.AdminReport
{
    public class AdminReportService : IAdminReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<IEnumerable<MonthlyRegistrationsDto>> GetUserRegistrationsAsync(int months = 12)
        {
            var since = MonthsAgoStart(months);

            var clientRows = _unitOfWork.Repository<Client, long>().GetQueryable()
                .Where(c => c.CreatedAt >= since)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var vendorRows = _unitOfWork.Repository<Vendor, long>().GetQueryable()
                .Where(v => v.CreatedAt >= since)
                .GroupBy(v => new { v.CreatedAt.Year, v.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var buckets = new Dictionary<(int Year, int Month), MonthlyRegistrationsDto>();

            foreach (var row in clientRows)
            {
                var key = (row.Year, row.Month);
                var bucket = GetOrAddBucket(buckets, key);
                bucket.ClientCount = row.Count;
            }

            foreach (var row in vendorRows)
            {
                var key = (row.Year, row.Month);
                var bucket = GetOrAddBucket(buckets, key);
                bucket.VendorCount = row.Count;
            }

            foreach (var bucket in buckets.Values)
            {
                bucket.TotalCount = bucket.ClientCount + bucket.VendorCount;
            }

            IEnumerable<MonthlyRegistrationsDto> result = buckets.Values
                .OrderBy(b => b.Year).ThenBy(b => b.Month)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<IEnumerable<MonthlyCountDto>> GetBookingsMonthlyAsync(int months = 12)
        {
            var since = MonthsAgoStart(months);

            IEnumerable<MonthlyCountDto> result = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .Where(b => b.CreatedAt >= since)
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => new MonthlyCountDto { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
                .OrderBy(row => row.Year).ThenBy(row => row.Month)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<IEnumerable<MonthlyAmountDto>> GetRevenueMonthlyAsync(int months = 12)
        {
            var since = MonthsAgoStart(months);

            IEnumerable<MonthlyAmountDto> result = _unitOfWork.Repository<Payment, long>().GetQueryable()
                .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt != null && p.PaidAt >= since)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new MonthlyAmountDto { Year = g.Key.Year, Month = g.Key.Month, Amount = g.Sum(p => p.Amount) })
                .OrderBy(row => row.Year).ThenBy(row => row.Month)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<IEnumerable<TopVendorDto>> GetTopVendorsAsync(string? by = "revenue", int take = 10)
        {
            take = take is < 1 or > 50 ? 10 : take;

            var revenueByVendor = _unitOfWork.Repository<Payment, long>().GetQueryable()
                .Where(p => p.Status == PaymentStatus.Completed)
                .GroupBy(p => p.VendorId)
                .Select(g => new { VendorId = g.Key, Revenue = g.Sum(p => p.Amount) })
                .ToDictionary(row => row.VendorId, row => row.Revenue);

            var bookingCountByVendor = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .GroupBy(b => b.VendorId)
                .Select(g => new { VendorId = g.Key, Count = g.Count() })
                .ToDictionary(row => row.VendorId, row => row.Count);

            var vendorIds = revenueByVendor.Keys.Union(bookingCountByVendor.Keys).ToList();

            var vendorNames = _unitOfWork.Repository<Vendor, long>().GetQueryable()
                .Where(v => vendorIds.Contains(v.Id))
                .Select(v => new { v.Id, v.BusinessName })
                .ToDictionary(row => row.Id, row => row.BusinessName);

            var rows = vendorIds.Select(id => new TopVendorDto
            {
                VendorId = id,
                BusinessName = vendorNames.TryGetValue(id, out var name) ? name : "Unknown",
                Revenue = revenueByVendor.TryGetValue(id, out var revenue) ? revenue : 0m,
                BookingCount = bookingCountByVendor.TryGetValue(id, out var count) ? count : 0
            });

            rows = string.Equals(by?.Trim(), "bookings", StringComparison.OrdinalIgnoreCase)
                ? rows.OrderByDescending(row => row.BookingCount)
                : rows.OrderByDescending(row => row.Revenue);

            IEnumerable<TopVendorDto> result = rows.Take(take).ToList();
            return Task.FromResult(result);
        }

        public Task<IEnumerable<TopCategoryDto>> GetTopCategoriesAsync(string? by = "vendors", int take = 10)
        {
            take = take is < 1 or > 50 ? 10 : take;

            var vendorCountByCategory = _unitOfWork.Repository<Vendor, long>().GetQueryable()
                .Where(v => v.CategoryId != null)
                .GroupBy(v => v.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionary(row => row.CategoryId, row => row.Count);

            var bookingCountByCategory = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .Where(b => b.Vendor.CategoryId != null)
                .GroupBy(b => b.Vendor.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionary(row => row.CategoryId, row => row.Count);

            var categoryIds = vendorCountByCategory.Keys.Union(bookingCountByCategory.Keys).ToList();

            var categoryNames = _unitOfWork.Repository<ServiceCategory, long>().GetQueryable()
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.NameEn })
                .ToDictionary(row => row.Id, row => row.NameEn);

            var rows = categoryIds.Select(id => new TopCategoryDto
            {
                CategoryId = id,
                CategoryName = categoryNames.TryGetValue(id, out var name) ? name : "Unknown",
                VendorCount = vendorCountByCategory.TryGetValue(id, out var vendorCount) ? vendorCount : 0,
                BookingCount = bookingCountByCategory.TryGetValue(id, out var bookingCount) ? bookingCount : 0
            });

            rows = string.Equals(by?.Trim(), "bookings", StringComparison.OrdinalIgnoreCase)
                ? rows.OrderByDescending(row => row.BookingCount)
                : rows.OrderByDescending(row => row.VendorCount);

            IEnumerable<TopCategoryDto> result = rows.Take(take).ToList();
            return Task.FromResult(result);
        }

        public Task<VendorVerificationFunnelDto> GetVendorVerificationFunnelAsync()
        {
            var verificationQueryable = _unitOfWork.Repository<VendorVerification, long>().GetQueryable();

            var funnel = new VendorVerificationFunnelDto
            {
                Submitted = verificationQueryable.Count(v => v.SubmittedAt != null),
                Pending = verificationQueryable.Count(v => v.IsCurrent && v.Status == VerificationStatus.Pending),
                Approved = verificationQueryable.Count(v => v.IsCurrent &&
                    (v.Status == VerificationStatus.Verified || v.Status == VerificationStatus.Trusted)),
                Rejected = verificationQueryable.Count(v => v.IsCurrent && v.Status == VerificationStatus.Rejected)
            };

            return Task.FromResult(funnel);
        }

        private static DateTimeOffset MonthsAgoStart(int months)
        {
            months = months is < 1 or > 36 ? 12 : months;
            var now = DateTimeOffset.UtcNow;
            var firstOfThisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            return firstOfThisMonth.AddMonths(-(months - 1));
        }

        private static MonthlyRegistrationsDto GetOrAddBucket(
            Dictionary<(int Year, int Month), MonthlyRegistrationsDto> buckets,
            (int Year, int Month) key)
        {
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new MonthlyRegistrationsDto { Year = key.Year, Month = key.Month };
                buckets[key] = bucket;
            }

            return bucket;
        }
    }
}
