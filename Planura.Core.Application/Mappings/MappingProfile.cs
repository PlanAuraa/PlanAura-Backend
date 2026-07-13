using AutoMapper;
using Planura.Core.Application.Models;
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

        CreateMap<BookingRequest, BookingRequestDto>();

        CreateMap<EventPlan, EventPlanDto>();
    }
}
