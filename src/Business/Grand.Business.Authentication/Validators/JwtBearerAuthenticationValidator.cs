using FluentValidation;
using Grand.Business.Core.Interfaces.Customers;
using Grand.Domain.Customers;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Grand.Business.Authentication.Validators;

/// In the context of validating a JWT bearer token, the system must ensure that:
/// 1. The principal must not be null.
/// 2. The token extracted from the claims must not be null when rule 1 holds true.
/// 3. The token extracted from the claims must not be an empty string when rule 2 holds true.
/// 4. The email extracted from the claims must not be null when rule 1 holds true.
/// 5. The email extracted from the claims must not be an empty string when rule 4 holds true.
/// 6. The customer obtained using the customer service must not be null when rule 4 holds true.
/// 7. The customer obtained using the customer service must be active when rule 6 holds true.
/// 8. The customer obtained using the customer service must not be marked as deleted when rule 6 holds true.
/// 9. The user API obtained by email using user api service must not be null when rule 5 holds true.
/// 10. The user API must not be null when rule 5 holds true.
/// 11. The user API must be active when rule 10 holds true.
/// 12. The user API token must match the token from the claims when rules 3,11 hold true.
public record JwtBearerAuthenticationContext(ClaimsPrincipal Principal, ICustomerService CustomerService, IUserApiService UserApiService);

public class JwtBearerAuthenticationValidator : AbstractValidator<JwtBearerAuthenticationContext>
{
    private Customer _customer;
    private UserApi _userApi;

    public JwtBearerAuthenticationValidator()
    {
        RuleFor(Principal).NotNull();

        RuleFor(Token).NotEmpty().WithMessage("Wrong token, change password on the customer and create token again").When(PrincipalIsNotNull).OverridePropertyName("Token");

        RuleFor(Email).NotEmpty().WithMessage("Email not exists in the context").When(PrincipalIsNotNull).OverridePropertyName("Email").DependentRules(() =>
        {
            RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                .MustAsync(CustomerIsNotNull).WithMessage("Email not exists/or not active in the customer table")
                .MustAsync(CustomerIsActive).WithMessage("Email not exists/or not active in the customer table")
                .MustAsync(CustomerIsNotDeleted).WithMessage("Email not exists/or not active in the customer table");

            RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                .MustAsync(UserApiIsNotNull).WithMessage("User api not exists/or not active in the user api table")
                .MustAsync(UserApiIsActive).WithMessage("User api not exists/or not active in the user api table")
                .MustAsync(UserApiTokenMatchesClaimsToken).WithMessage("Wrong token, generate again");
        });
    }

    private static readonly Expression<Func<JwtBearerAuthenticationContext, JwtBearerAuthenticationContext>> WholeContext = context => context;

    private static readonly Expression<Func<JwtBearerAuthenticationContext, ClaimsPrincipal>> Principal = context => context.Principal;

    private static readonly Expression<Func<JwtBearerAuthenticationContext, string>> Token = context => TokenFunc(context);

    private static readonly Expression<Func<JwtBearerAuthenticationContext, string>> Email = context => EmailFunc(context);

    private static readonly Func<JwtBearerAuthenticationContext, string> TokenFunc = context => context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "Token")?.Value;

    private static readonly Func<JwtBearerAuthenticationContext, string> EmailFunc = context => context.Principal.Claims.ToList().FirstOrDefault(x => x.Type == "Email")?.Value;

    private static bool PrincipalIsNotNull(JwtBearerAuthenticationContext context) => context.Principal != null;

    private async Task<bool> CustomerIsNotNull(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        var customer = await GetCustomer(context.CustomerService, EmailFunc(context));
        return customer != null;
    }

    private async Task<bool> CustomerIsActive(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        var customer = await GetCustomer(context.CustomerService, EmailFunc(context));
        return customer.Active;
    }

    private async Task<bool> CustomerIsNotDeleted(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        var customer = await GetCustomer(context.CustomerService, EmailFunc(context));
        return !customer.Deleted;
    }

    private async Task<bool> UserApiIsNotNull(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        _userApi = await GetUserApi(context.UserApiService, EmailFunc(context));
        return _userApi != null;
    }

    private async Task<bool> UserApiIsActive(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        var userApi = await GetUserApi(context.UserApiService, EmailFunc(context));
        return userApi.IsActive;
    }

    private async Task<bool> UserApiTokenMatchesClaimsToken(JwtBearerAuthenticationContext context, CancellationToken _)
    {
        var userApi = await GetUserApi(context.UserApiService, EmailFunc(context));
        var token = TokenFunc(context);
        return userApi.Token == token;
    }

    private async Task<Customer> GetCustomer(ICustomerService customerService, string email) => _customer ??= await customerService.GetCustomerByEmail(email);

    private async Task<UserApi> GetUserApi(IUserApiService userApiService, string email) => _userApi ??= await userApiService.GetUserByEmail(email);
}