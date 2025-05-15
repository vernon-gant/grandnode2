using AutoMapper;
using Grand.Domain.Catalog;
using Grand.Infrastructure.Mapper;
using Grand.Module.Api.DTOs.Catalog;

namespace Grand.Module.Api.Infrastructure.Mapper.Profiles;

public class TierPriceProfile : Profile, IAutoMapperProfile
{
    public TierPriceProfile()
    {
        CreateMap<ProductTierPriceDto, TierPrice>();

        CreateMap<TierPrice, ProductTierPriceDto>();
    }

    public int Order => 1;
}