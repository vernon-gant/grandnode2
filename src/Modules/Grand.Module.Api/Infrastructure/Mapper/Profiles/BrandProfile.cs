using AutoMapper;
using Grand.Domain.Catalog;
using Grand.Infrastructure.Mapper;
using Grand.Module.Api.DTOs.Catalog;

namespace Grand.Module.Api.Infrastructure.Mapper.Profiles;

public class BrandProfile : Profile, IAutoMapperProfile
{
    public BrandProfile()
    {
        CreateMap<BrandDto, Brand>()
            .ForMember(dest => dest.LimitedToGroups, mo => mo.Ignore())
            .ForMember(dest => dest.CustomerGroups, mo => mo.Ignore())
            .ForMember(dest => dest.LimitedToStores, mo => mo.Ignore())
            .ForMember(dest => dest.Stores, mo => mo.Ignore())
            .ForMember(dest => dest.CreatedOnUtc, mo => mo.Ignore())
            .ForMember(dest => dest.UpdatedOnUtc, mo => mo.Ignore())
            .ForMember(dest => dest.Locales, mo => mo.Ignore())
            .ForMember(dest => dest.AppliedDiscounts, mo => mo.Ignore())
            .ForMember(dest => dest.UserFields, mo => mo.Ignore());

        CreateMap<Brand, BrandDto>();
    }

    public int Order => 1;
}