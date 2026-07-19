using Planura.Core.Application.Models.AdminDashboard;
using Planura.Core.Application.Specifications.AdminDashboard;
using Planura.Core.Domain.Repositories;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Planura.Core.Application.Services.AdminDashboard
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        public AdminDashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
        {
            var dashboard = new DashboardStatisticsDto();
            var verificationRepository =
    _unitOfWork.Repository<Planura.Core.Domain.Entities.VendorVerification, long>();

            dashboard.TotalVendors = await _unitOfWork
                .Repository<Planura.Core.Domain.Entities.Vendor, long>()
                .GetCountAsync(new AllVendorsSpecification());

            dashboard.PendingVendors =
    await verificationRepository.GetCountAsync(new PendingVendorCountSpecification());

            dashboard.ApprovedVendors =
                await verificationRepository.GetCountAsync(new ApprovedVendorCountSpecification());

            dashboard.RejectedVendors =
                await verificationRepository.GetCountAsync(new RejectedVendorCountSpecification());

            dashboard.UnverifiedVendors = Math.Max(
                0,
                dashboard.TotalVendors - dashboard.PendingVendors - dashboard.ApprovedVendors - dashboard.RejectedVendors);

            dashboard.TotalClients = await _unitOfWork
                .Repository<Planura.Core.Domain.Entities.Client, long>()
                .GetCountAsync(new AllClientsSpecification());

            dashboard.TotalBookingRequests = await _unitOfWork
                .Repository<BookingRequest, long>()
                .GetCountAsync(new AllBookingRequestsSpecification());

            dashboard.TotalRevenue = await _unitOfWork
    .Repository<Planura.Core.Domain.Entities.Payment, long>()
    .GetSumAsync(
        new PaidPaymentsSpecification(),
        p => p.Amount);

            var now = DateTimeOffset.UtcNow;
            var weekAgo = now.AddDays(-7);
            var thirtyDaysAgo = now.AddDays(-30);

            dashboard.OpenDisputes = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .Count(b => b.DisputeStatus == DisputeStatus.Open);

            dashboard.NewClientsThisWeek = _unitOfWork.Repository<Client, long>().GetQueryable()
                .Count(c => c.CreatedAt >= weekAgo);

            dashboard.NewVendorsThisWeek = _unitOfWork.Repository<Vendor, long>().GetQueryable()
                .Count(v => v.CreatedAt >= weekAgo);

            dashboard.ActiveUsersLast30Days = _unitOfWork.Repository<ApplicationUser, long>().GetQueryable()
                .Count(u => u.LastLoginAt != null && u.LastLoginAt >= thirtyDaysAgo);

            dashboard.RevenueThisMonth = _unitOfWork.Repository<Payment, long>().GetQueryable()
                .Where(p => p.Status == PaymentStatus.Completed
                    && p.PaidAt != null
                    && p.PaidAt.Value.Year == now.Year
                    && p.PaidAt.Value.Month == now.Month)
                .Sum(p => (decimal?)p.Amount) ?? 0m;

            dashboard.BookingsByStatus = _unitOfWork.Repository<BookingRequest, long>().GetQueryable()
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionary(row => row.Status.ToString(), row => row.Count);

            return dashboard;
        }

        public async Task<IEnumerable<RecentActivityItemDto>> GetRecentActivityAsync(int take = 20)
        {
            if (take is < 1 or > 100)
            {
                take = 20;
            }

            var verificationEvents = _unitOfWork.Repository<VendorVerificationHistory, long>().GetQueryable()
                .OrderByDescending(h => h.ChangedAt)
                .Take(take)
                .Select(h => new RecentActivityItemDto
                {
                    EventType = "VendorVerification",
                    Description = "Vendor verification " + (h.PreviousStatus ?? "new") + " -> " + h.NewStatus
                        + (h.Notes != null ? " (" + h.Notes + ")" : string.Empty),
                    ActorName = h.ChangedByAdmin != null ? h.ChangedByAdmin.FullName : null,
                    OccurredAt = h.ChangedAt
                })
                .ToList();

            var bookingEvents = _unitOfWork.Repository<BookingStatusHistory, long>().GetQueryable()
                .OrderByDescending(h => h.ChangedAt)
                .Take(take)
                .Select(h => new RecentActivityItemDto
                {
                    EventType = "BookingStatus",
                    Description = "Booking #" + h.BookingRequestId + " " + (h.PreviousStatus ?? "created") + " -> " + h.NewStatus
                        + (h.Notes != null ? " (" + h.Notes + ")" : string.Empty),
                    ActorName = h.ChangedByUser != null ? h.ChangedByUser.FullName : null,
                    OccurredAt = h.ChangedAt
                })
                .ToList();

            var merged = verificationEvents
                .Concat(bookingEvents)
                .OrderByDescending(e => e.OccurredAt)
                .Take(take)
                .ToList();

            return await Task.FromResult(merged);
        }
    }
}
