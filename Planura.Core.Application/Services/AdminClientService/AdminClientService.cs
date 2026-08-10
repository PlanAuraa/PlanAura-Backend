using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminClient;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AdminClient
{
    /// <summary>
    /// Backs the "Client Management" admin page (AdminDashboardPlan.md 2.5) - ClientController is
    /// entirely self-service (GET/PUT me, ClientOnly), so this is the first admin-facing surface
    /// over the client side of the marketplace.
    /// </summary>
    public class AdminClientService : IAdminClientService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminClientService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<AdminClientListItemDto>> ListClientsAsync(AdminClientFilterDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            var queryable = _unitOfWork.Repository<Client, long>().GetQueryable();

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                var city = filter.City.Trim().ToLower();
                queryable = queryable.Where(c => c.City != null && c.City.ToLower().Contains(city));
            }

            if (filter.IsAccountActive is not null)
            {
                queryable = queryable.Where(c => c.User.IsActive == filter.IsAccountActive);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim().ToLower();
                queryable = queryable.Where(c =>
                    c.User.FullName.ToLower().Contains(search) ||
                    (c.User.Email != null && c.User.Email.ToLower().Contains(search)) ||
                    (c.User.PhoneNumber != null && c.User.PhoneNumber.Contains(search)));
            }

            var totalCount = queryable.Count();

            var items = queryable
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new AdminClientListItemDto
                {
                    ClientId = c.Id,
                    UserId = c.UserId,
                    FullName = c.User.FullName,
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    City = c.City,
                    IsAccountActive = c.User.IsActive,
                    BookingCount = c.BookingRequests.Count,
                    CreatedAt = c.CreatedAt
                })
                .ToList();

            return Task.FromResult(new PagedResult<AdminClientListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<AdminClientDetailsDto> GetClientDetailsAsync(long clientId)
        {
            var client = _unitOfWork.Repository<Client, long>().GetQueryable()
                .Where(c => c.Id == clientId)
                .Select(c => new AdminClientDetailsDto
                {
                    ClientId = c.Id,
                    UserId = c.UserId,
                    FullName = c.User.FullName,
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    City = c.City,
                    DateOfBirth = c.DateOfBirth,
                    IsAccountActive = c.User.IsActive,
                    CreatedAt = c.CreatedAt,
                    EventPlanCount = c.EventPlans.Count,
                    BookingCount = c.BookingRequests.Count,
                    EventPlans = c.EventPlans
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(20)
                        .Select(p => new AdminClientEventPlanDto
                        {
                            Id = p.Id,
                            Title = p.Title,
                            EventType = p.EventType,
                            EventDate = p.EventDate,
                            Status = p.Status,
                            CreatedAt = p.CreatedAt
                        }).ToList(),
                    Bookings = c.BookingRequests
                        .OrderByDescending(b => b.CreatedAt)
                        .Take(20)
                        .Select(b => new AdminClientBookingDto
                        {
                            Id = b.Id,
                            VendorName = b.Vendor.BusinessName,
                            EventDate = b.EventDate,
                            AgreedPrice = b.AgreedPrice,
                            Status = b.Status.ToString(),
                            PaymentStatus = b.PaymentStatus.ToString()
                        }).ToList()
                })
                .FirstOrDefault();

            if (client is null)
            {
                throw new NotFoundExeption(nameof(Client), clientId);
            }

            // Fully-captured spend: full path (Completed) + deposit path once its remainder was collected
            // (FullyPaid). TotalAmount is the full captured price on the deposit path (Amount is only the
            // deposit); fall back to Amount for legacy rows. Refunded is excluded.
            client.TotalSpend = _unitOfWork.Repository<Payment, long>().GetQueryable()
                .Where(p => p.ClientId == clientId
                    && (p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.FullyPaid))
                .Sum(p => (decimal?)(p.TotalAmount ?? p.Amount)) ?? 0m;

            return await Task.FromResult(client);
        }
    }
}
