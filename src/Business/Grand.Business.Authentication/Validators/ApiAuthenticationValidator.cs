using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Customers;
using Grand.Domain.Customers;
using Grand.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Linq.Expressions;

namespace Grand.Business.Authentication.Validators;

/// In the context of API authentication, the system must ensure that:
/// 1. The HTTP context retrieved from http context accessor is not null and return null customer in case of failure.
/// 2. The authorization header is set when rule 1 holds true and return null customer in case of failure.
/// 3. The validator must return API customer when the API front is authenticated with the check performed using the customer service and rule 2 holds true.
/// 4. The authentication must be successful when rule 2 holds true and return null customer in case of failure.
/// 5. The customer must be active when rule 4 holds true and return null customer in case of failure.
/// 6. The customer must not be deleted when rule 5 holds true and return null customer in case of failure.
/// 7. The customer must be registered with the check performed using the group service when rule 6 holds true and return null customer in case of failure.
/// 8. The validator must return email customer when the email claim retrieved using the http context accessor is set and rule 7 holds true.
/// 9. The validator must return null customer when rule 8 does not hold true.
public record ApiAuthenticationContext(ICustomerService CustomerService, IGroupService GroupService, IHttpContextAccessor HttpContextAccessor);

public class ApiAuthenticationValidator : AbstractValidator<ApiAuthenticationContext>
{
    private Customer _apiCustomer;
    private Customer _emailCustomer;

    public ApiAuthenticationValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNonNullHttpContext).WithState(NullCustomer)
            .Must(HaveSetAuthorizationHeader).WithState(NullCustomer)
            .OverridePropertyName(WholeContextPropertyName)
            .DependentRules(() =>
            {
                RuleFor(WholeContext).Must(ReturnCustomer).WhenAsync(IsApiFrontAuthenticated).WithState(ApiCustomer).OverridePropertyName(WholeContextPropertyName);

                RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                    .MustAsync(AuthenticateUsingHttpContextSuccessfully).WithState(NullCustomer)
                    .MustAsync(HaveActiveNotDeletedAndRegisteredCustomer).WithState(NullCustomer)
                    .Must(ReturnCustomer).WhenAsync(CustomerEmailClaimIsSet).WithState(EmailCustomer)
                    .Must(ReturnCustomer).WithState(NullCustomer)
                    .OverridePropertyName(WholeContextPropertyName);
            });
    }

    private const string WholeContextPropertyName = "WholeContext";

    private static Expression<Func<ApiAuthenticationContext, ApiAuthenticationContext>> WholeContext => context => context;

    private static bool HaveNonNullHttpContext(ApiAuthenticationContext context) => context.HttpContextAccessor.HttpContext != null;

    private static bool HaveSetAuthorizationHeader(ApiAuthenticationContext context) => context.HttpContextAccessor.HttpContext!.Request.Headers.ContainsKey(HeaderNames.Authorization);

    private async Task<Customer> GetApiCustomer(IHttpContextAccessor httpContextAccessor, ICustomerService customerService)
    {
        var authResult = await httpContextAccessor.HttpContext!.AuthenticateAsync(FrontendAPIConfig.AuthenticationScheme);

        if (!authResult.Succeeded)
            return await customerService.GetCustomerBySystemName(SystemCustomerNames.Anonymous);

        var email = authResult.Principal.Claims.FirstOrDefault(x => x.Type == "Email")?.Value;
        var id = authResult.Principal.Claims.FirstOrDefault(x => x.Type == "Guid")?.Value;

        if (email is not null) return await customerService.GetCustomerByEmail(email);

        return id is not null ? await customerService.GetCustomerByGuid(Guid.Parse(id)) : null;
    }

    private async Task<bool> IsApiFrontAuthenticated(ApiAuthenticationContext context, CancellationToken _)
    {
        var isApiFrontAuthenticated = context.HttpContextAccessor.HttpContext?
                                          .GetEndpoint()?
                                          .Metadata
                                          .GetOrderedMetadata<AuthorizeAttribute>()
                                          .Any(attr => attr.AuthenticationSchemes?.Contains(FrontendAPIConfig.AuthenticationScheme) == true)
                                      ?? false;

        if (isApiFrontAuthenticated) _apiCustomer = await GetApiCustomer(context.HttpContextAccessor, context.CustomerService);

        return isApiFrontAuthenticated;
    }

    private async Task<bool> AuthenticateUsingHttpContextSuccessfully(ApiAuthenticationContext context, CancellationToken _)
    {
        var authenticateResult = await context.HttpContextAccessor.HttpContext!.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        return authenticateResult.Succeeded;
    }

    private async Task<bool> HaveActiveNotDeletedAndRegisteredCustomer(ApiAuthenticationContext context, CancellationToken _) => _apiCustomer.Active && !_apiCustomer.Deleted && await context.GroupService.IsRegistered(_apiCustomer);

    private async Task<bool> CustomerEmailClaimIsSet(ApiAuthenticationContext context, CancellationToken _)
    {
        var emailClaim = context.HttpContextAccessor.HttpContext!.User.Claims.FirstOrDefault(claim => claim.Type == "Email");

        if (emailClaim != null)
            _emailCustomer = await context.CustomerService.GetCustomerByEmail(emailClaim.Value);

        return _emailCustomer != null;
    }

    private static bool ReturnCustomer(ApiAuthenticationContext context) => false;

    private static object NullCustomer(ApiAuthenticationContext context) => null;

    private object ApiCustomer(ApiAuthenticationContext context) => _apiCustomer;

    private object EmailCustomer(ApiAuthenticationContext context) => _emailCustomer;
}