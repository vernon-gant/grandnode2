using Grand.Business.Authentication.Validators;
using Grand.Business.Core.Interfaces.Authentication;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Business.Core.Interfaces.Customers;
using Grand.Domain.Customers;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Grand.Business.Authentication.Services;

public class JwtBearerCustomerAuthenticationService : IJwtBearerCustomerAuthenticationService
{
    private readonly ICustomerService _customerService;
    private readonly IGroupService _groupService;
    private readonly IPermissionService _permissionService;
    private readonly IRefreshTokenService _refreshTokenService;
    private string _errorMessage;

    public JwtBearerCustomerAuthenticationService(ICustomerService customerService,
        IPermissionService permissionService, IGroupService groupService, IRefreshTokenService refreshTokenService)
    {
        _customerService = customerService;
        _permissionService = permissionService;
        _groupService = groupService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<bool> Valid(TokenValidatedContext context)
    {
        if (context.Principal == null) return false;
        var email = context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "Email")?.Value;
        var passwordToken = context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "Token")?.Value;
        var refreshId = context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "RefreshId")?.Value;
        var id = context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "Id")?.Value;
        Customer customer = email is null ? await _customerService.GetCustomerByGuid(Guid.Parse(id)) : await _customerService.GetCustomerByEmail(email);
        var validationContext = new JwtBearerCustomerAuthenticationContext(customer, passwordToken, refreshId, _groupService, _refreshTokenService, _permissionService);
        var result = await new JwtBearerCustomerAuthenticationValidator().ValidateAsync(validationContext);

        if (!result.IsValid) _errorMessage = result.Errors.First().ErrorMessage;

        return result.IsValid;
    }


    public Task<string> ErrorMessage()
    {
        return Task.FromResult(_errorMessage);
    }
}