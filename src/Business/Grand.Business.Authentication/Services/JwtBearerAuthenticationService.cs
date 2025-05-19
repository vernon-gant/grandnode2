using Grand.Business.Authentication.Validators;
using Grand.Business.Core.Interfaces.Authentication;
using Grand.Business.Core.Interfaces.Customers;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Grand.Business.Authentication.Services;

public class JwtBearerAuthenticationService : IJwtBearerAuthenticationService
{
    private readonly ICustomerService _customerService;
    private readonly IUserApiService _userApiService;
    private string _email;

    private string _errorMessage;

    public JwtBearerAuthenticationService(
        ICustomerService customerService, IUserApiService userApiService)
    {
        _customerService = customerService;
        _userApiService = userApiService;
    }

    /// <summary>
    ///     Valid
    /// </summary>
    /// <param name="context">Context</param>
    public virtual async Task<bool> Valid(TokenValidatedContext context)
    {
        var validationContext = new JwtBearerAuthenticationContext(context.Principal, _customerService, _userApiService);
        var result = await new JwtBearerAuthenticationValidator().ValidateAsync(validationContext);

        if (!result.IsValid)
            _errorMessage = result.Errors.First().ErrorMessage;

        return result.IsValid;
    }

    /// <summary>
    ///     Get error message
    /// </summary>
    /// <returns></returns>
    public virtual Task<string> ErrorMessage()
    {
        return Task.FromResult(_errorMessage);
    }
}