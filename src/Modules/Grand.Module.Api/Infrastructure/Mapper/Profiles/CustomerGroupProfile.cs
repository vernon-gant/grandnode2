using AutoMapper;
using Grand.Domain.Customers;
using Grand.Infrastructure.Mapper;
using Grand.Module.Api.DTOs.Customers;

namespace Grand.Module.Api.Infrastructure.Mapper.Profiles;

public class CustomerGroupProfile : Profile, IAutoMapperProfile
{
    public CustomerGroupProfile()
    {
        CreateMap<CustomerGroupDto, CustomerGroup>()
            .ForMember(dest => dest.UserFields, mo => mo.Ignore());

        CreateMap<CustomerGroup, CustomerGroupDto>();
    }

    public int Order => 1;
}