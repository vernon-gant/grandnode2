using Grand.Business.Authentication.Validators;
using Grand.Business.Core.Interfaces.Authentication;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Customers;
using Grand.Domain.Customers;
using Microsoft.AspNetCore.Http;

namespace Grand.Business.Authentication.Services;

public class ApiAuthenticationService : IApiAuthenticationService
{
    private readonly ICustomerService _customerService;
    private readonly IGroupService _groupService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiAuthenticationService(ICustomerService customerService, IGroupService groupService, IHttpContextAccessor httpContextAccessor)
    {
        _customerService = customerService;
        _groupService = groupService;
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<Customer> GetAuthenticatedCustomer()
    {
        var authenticationContext = new ApiAuthenticationContext(_customerService, _groupService, _httpContextAccessor);
        var authenticationResult = await new ApiAuthenticationValidator().ValidateAsync(authenticationContext);
        return (Customer)authenticationResult.Errors.First().CustomState;
    }
}