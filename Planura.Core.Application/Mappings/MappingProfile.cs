using AutoMapper;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models;
using Planura.Core.Application.Models.AdminBooking;
using Planura.Core.Domain.Entities;

namespace Planura.Core.Application.Mappings;

public class MappingProfile : Profile
{
    // Local alias so the LINQ projection below reads cleanly; the single source of truth is
    // BookingStatusHistoryNotes, shared with the code that writes the note.
    private const string DisputeRaisedNotePrefix = BookingStatusHistoryNotes.DisputeRaisedPrefix;

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
            // Payment is projected by BookingService.BuildPaymentSummary, which needs the configured
            // currency and the authorized-vs-captured distinction - neither available to a plain map.
            .ForMember(d => d.Payment, opt => opt.Ignore())
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
    // The reason lives in the BookingStatusHistory entry FlagDisputeAsync writes as
    // "Dispute raised: {reason}" - there is no dedicated column. A booking can be disputed
    // more than once (a resolved dispute can be reopened), so take the most recent such entry.
    // StatusHistory is eager-loaded by DisputeDetailsSpecification, so this runs in memory.
    d => d.DisputeReason,
    opt => opt.MapFrom(s =>
        s.StatusHistory
            .Where(h => h.Notes != null && h.Notes.StartsWith(DisputeRaisedNotePrefix))
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => h.Notes!.Substring(DisputeRaisedNotePrefix.Length))
            .FirstOrDefault()))
            .ForMember(
    d => d.DisputeRaisedBy,
    opt => opt.MapFrom(s =>
        s.DisputedByUserId == null ? null
        : s.DisputedByUserId == s.Client.UserId ? "Client"
        : s.DisputedByUserId == s.Vendor.UserId ? "Vendor"
        : null))
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
