using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminVendorPayout;
using Planura.Core.Application.Specifications;
using Planura.Core.Application.Specifications.VendorPayout;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AdminVendorPayoutService
{
    /// <summary>
    /// Backs the "Vendor Payables" admin surface: how much each vendor is owed, and manually recording the
    /// out-of-band payouts that settle it. There is no automated payout gateway - the platform doesn't move
    /// money to vendors itself, so this is a settlement ledger (VendorPayout) the admin maintains by hand.
    /// </summary>
    public class AdminVendorPayoutService : IAdminVendorPayoutService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminVendorPayoutService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<VendorFinancialSummaryDto>> ListVendorFinancialsAsync(VendorFinancialFilterDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            var vendorQueryable = _unitOfWork.Repository<Vendor, long>().GetQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                vendorQueryable = vendorQueryable.Where(v => v.BusinessName.Contains(filter.Search));
            }

            var totalCount = vendorQueryable.Count();

            var vendors = vendorQueryable
                .OrderBy(v => v.BusinessName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<VendorFinancialSummaryDto>(vendors.Count);
            foreach (var vendor in vendors)
            {
                items.Add(await BuildVendorFinancialAsync(vendor));
            }

            return new PagedResult<VendorFinancialSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<VendorFinancialSummaryDto> GetVendorFinancialAsync(long vendorId)
        {
            var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
            if (vendor is null)
            {
                throw new NotFoundExeption(nameof(Vendor), vendorId);
            }

            return await BuildVendorFinancialAsync(vendor);
        }

        public async Task<VendorPayoutDto> RecordPayoutAsync(long vendorId, long adminId, RecordVendorPayoutDto dto)
        {
            var vendor = await _unitOfWork.Repository<Vendor, long>().GetAsync(vendorId);
            if (vendor is null)
            {
                throw new NotFoundExeption(nameof(Vendor), vendorId);
            }

            // Recomputed server-side, never trusted from the client - a payout can never exceed what is
            // currently owed, which keeps a typo from putting the ledger into a negative balance.
            var financial = await BuildVendorFinancialAsync(vendor);
            if (dto.Amount > financial.AmountPayable)
            {
                throw new BadRequestExeption(
                    $"Payout of {dto.Amount} exceeds the amount payable to this vendor ({financial.AmountPayable}).");
            }

            var payout = new Domain.Entities.VendorPayout
            {
                VendorId = vendorId,
                Amount = dto.Amount,
                PayoutDate = dto.PayoutDate,
                Reference = dto.Reference,
                Notes = dto.Notes,
                RecordedByAdminId = adminId
            };

            var repo = _unitOfWork.Repository<Domain.Entities.VendorPayout, long>();
            await repo.AddAsync(payout);
            await _unitOfWork.SaveChangesAsync();

            var admin = await _unitOfWork.Repository<ApplicationUser, long>().GetAsync(adminId);

            return new VendorPayoutDto
            {
                Id = payout.Id,
                VendorId = payout.VendorId,
                Amount = payout.Amount,
                PayoutDate = payout.PayoutDate,
                Reference = payout.Reference,
                Notes = payout.Notes,
                RecordedByAdminName = admin?.FullName,
                CreatedAt = payout.CreatedAt
            };
        }

        public async Task<List<VendorPayoutDto>> ListPayoutsAsync(long vendorId)
        {
            var payouts = await _unitOfWork.Repository<Domain.Entities.VendorPayout, long>()
                .GetAllWithSpecAsync(new VendorPayoutsByVendorSpecification(vendorId));

            return payouts.Select(p => new VendorPayoutDto
            {
                Id = p.Id,
                VendorId = p.VendorId,
                Amount = p.Amount,
                PayoutDate = p.PayoutDate,
                Reference = p.Reference,
                Notes = p.Notes,
                RecordedByAdminName = p.RecordedByAdmin?.FullName,
                CreatedAt = p.CreatedAt
            }).ToList();
        }

        private async Task<VendorFinancialSummaryDto> BuildVendorFinancialAsync(Vendor vendor)
        {
            var bookingQueryable = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .Where(b => b.VendorId == vendor.Id);

            var totalBookings = bookingQueryable.Count();
            var totalBookingValue = bookingQueryable.Sum(b => (decimal?)b.AgreedPrice) ?? 0m;

            var paymentRepo = _unitOfWork.Repository<Domain.Entities.Payment, long>();
            var revenueSpec = new RevenuePaymentsByVendorSpecification(vendor.Id);
            var totalCollected = await paymentRepo.GetSumAsync(revenueSpec, Domain.Entities.Payment.AmountCapturedExpression);
            var totalRefunded = await paymentRepo.GetSumAsync(revenueSpec, p => p.RefundedAmount ?? 0m);

            var amountPaidOut = await _unitOfWork.Repository<Domain.Entities.VendorPayout, long>()
                .GetSumAsync(new VendorPayoutsByVendorSpecification(vendor.Id), p => p.Amount);

            var netCollected = totalCollected - totalRefunded;

            return new VendorFinancialSummaryDto
            {
                VendorId = vendor.Id,
                VendorName = vendor.BusinessName,
                TotalBookings = totalBookings,
                TotalBookingValue = totalBookingValue,
                TotalCollected = totalCollected,
                TotalRefunded = totalRefunded,
                NetCollected = netCollected,
                AmountPaidOut = amountPaidOut,
                AmountPayable = Math.Max(netCollected - amountPaidOut, 0m)
            };
        }
    }
}
