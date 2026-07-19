using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Application.Specifications.AdminBooking;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Enums;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AdminBooking
{
    public class AdminBookingService : IAdminBookingService
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public AdminBookingService(IUnitOfWork unitOfWork , IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<IEnumerable<AdminDisputeListItemDto>> GetOpenDisputesAsync()
{
            var spec = new OpenDisputesSpecification();
            var disputes = await _unitOfWork
                .Repository<BookingRequest,long>()
                .GetAllWithSpecAsync(spec);

            return _mapper.Map<IEnumerable<AdminDisputeListItemDto>>(disputes);
        }
        public async Task<AdminDisputeDetailsDto> GetDisputeDetailsAsync(long bookingId)
        {
            if (bookingId <= 0)
            {
                throw new BadRequestExeption("Booking id must be greater than zero.");
            }
            
                var spec = new DisputeDetailsSpecification(bookingId);
                var dispute = await _unitOfWork
                    .Repository<BookingRequest, long>()
                    .GetWithSpecAsync(spec);
                if(dispute == null)
                {
                throw new NotFoundExeption("Dispute", bookingId);
            }
                
                    return _mapper.Map<AdminDisputeDetailsDto>(dispute);
            
        }


        public async Task ResolveDisputeAsync(long bookingId, long adminId, ResolveDisputeDto dto)
        {
            var req = await _unitOfWork.Repository<BookingRequest, long>().GetAsync(bookingId);
            if (req == null) throw new NotFoundExeption(nameof(BookingRequest), bookingId);
            if ( req.DisputeStatus != DisputeStatus.Open)
            {
                throw new BadRequestExeption("Only open disputes can be resolved.");
            }
            req.DisputeStatus = DisputeStatus.Resolved;
            req.ResolutionNotes = dto.ResolutionNotes;
            req.ResolvedByAdminId = adminId;
            req.ResolvedAt = DateTimeOffset.UtcNow;
            req.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<BookingRequest, long>().Update(req);
            await _unitOfWork.SaveChangesAsync();


        }


        public async Task<PagedResult<AdminBookingDto>> GetBookingsAsync(AdminBookingFilterDto filter)
        {
            var specification = new AdminBookingsSpecification(filter);
            var listSpec = new AdminBookingsSpecification(filter);

            var countSpec = new AdminBookingsSpecification(filter, false);
            var bookings = await _unitOfWork
                .Repository<BookingRequest, long>()
                .GetAllWithSpecAsync(listSpec);

            var totalCount = await _unitOfWork
                .Repository<BookingRequest, long>()
                .GetCountAsync(countSpec);

            var bookingDtos = _mapper.Map<List<AdminBookingDto>>(bookings);

            return new PagedResult<AdminBookingDto>
            {
                Items = bookingDtos,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
    }
}
