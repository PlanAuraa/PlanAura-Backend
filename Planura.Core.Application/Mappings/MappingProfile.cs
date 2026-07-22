using AutoMapper;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Domain.Entities;

namespace Planura.Core.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ServiceCategory, ServiceCategoryDto>();
        CreateMap<CreateServiceCategoryDto, ServiceCategory>();
        CreateMap<UpdateServiceCategoryDto, ServiceCategory>();

        CreateMap<VendorPackage, VendorPackageDto>();
        CreateMap<CreateVendorPackageDto, VendorPackage>();
        CreateMap<UpdateVendorPackageDto, VendorPackage>();

        CreateMap<VendorAvailability, VendorAvailabilityDto>();

        CreateMap<BookingRequest, BookingRequestDto>()
            .ForMember(d => d.ReviewId, opt => opt.MapFrom(s => s.Review != null ? s.Review.Id : (long?)null))
            .ForMember(d => d.ReviewRating, opt => opt.MapFrom(s => s.Review != null ? (int?)s.Review.Rating : null))
            .ForMember(d => d.ReviewComment, opt => opt.MapFrom(s => s.Review != null ? s.Review.Comment : null));

        CreateMap<EventPlan, EventPlanDto>();

        CreateMap<Payment, PaymentDto>();

        CreateMap<AiChatMessage, AiChatMessageDto>();
        CreateMap<AiChatConversation, AiChatConversationDto>();


        CreateMap<BookingRequest, AdminDisputeListItemDto>()
            .ForMember(d => d.BookingId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.BookingStatus, opt => opt.MapFrom(s => s.Status))
            .ForMember(
    d => d.ClientName,
    opt => opt.MapFrom(s => s.Client.User.FullName))
            .ForMember(
    d => d.VendorName,
    opt => opt.MapFrom(s => s.Vendor.BusinessName));

        CreateMap<BookingRequest, AdminDisputeDetailsDto>()
            .ForMember(d => d.BookingId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.BookingStatus, opt => opt.MapFrom(s => s.Status))
            .ForMember(
    d => d.ClientName,
    opt => opt.MapFrom(s => s.Client.User.FullName))
            .ForMember(
    d => d.VendorName,
    opt => opt.MapFrom(s => s.Vendor.BusinessName));

        CreateMap<BookingRequest, AdminBookingDto>().ForMember(
    d => d.ClientName,
    opt => opt.MapFrom(s => s.Client.User.FullName))
            .ForMember(
    d => d.VendorName,
    opt => opt.MapFrom(s => s.Vendor.BusinessName))
            .ForMember(d=>d.PackageName,opt => opt.MapFrom(s=>s.VendorPackage.Title));


    }
}
