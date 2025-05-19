using FluentValidation;
using Grand.Business.Core.Interfaces.Authentication;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Common;
using Grand.Domain.Customers;
using Grand.Domain.Permissions;
using Grand.Domain.Security;
using System.Linq.Expressions;

namespace Grand.Business.Authentication.Validators;

/// In the context of validating a customer via JWT bearer token, the system must ensure that:
/// 1. The customer must not be null.
/// 2. The customer must be active when rule 1 holds true.
/// 3. The customer must not be marked as deleted when rule 2 holds true.
/// 4. The refresh token retrieved using the refresh token service must not be null when rule 3 holds true.
/// 5. The refresh id must not be null when rule 4 holds true.
/// 6. The refresh id must match the refresh token id when rule 5 hold true.
/// 7. The customer must have the permission to use api from the permission service when rule 6 holds true.
/// 8. The customer must pass the validation when customer is guest with the check performed using the group service and rule 7 holds true.
/// 9. The password token must be equal to the token stored on the customer when customer is not guest and rule 7 holds true.
public record JwtBearerCustomerAuthenticationContext(Customer Customer, string PasswordToken, string RefreshId, IGroupService GroupService, IRefreshTokenService RefreshTokenService, IPermissionService PermissionService);

public class JwtBearerCustomerAuthenticationValidator : AbstractValidator<JwtBearerCustomerAuthenticationContext>
{
    private RefreshToken _refreshToken;
    public JwtBearerCustomerAuthenticationValidator()
    {
        RuleFor(Customer).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Not found customer")
            .Must(BeActive).WithMessage("Customer not exists/or not active in the customer table")
            .Must(NotBeDeleted).WithMessage("Customer not exists/or not deleted in the customer table")
            .DependentRules(() =>
            {
                RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                    .MustAsync(HaveNonNullRefreshToken).WithMessage("Invalid token or cancel by refresh token")
                    .Must(HaveNonNullProvidedRefreshId).WithMessage("Invalid token or cancel by refresh token")
                    .MustAsync(HaveProvidedRefreshIdMatchingTheIdOfRefreshToken).WithMessage("Invalid token or cancel by refresh token")
                    .DependentRules(() =>
                    {
                        WhenAsync(CustomerHasPermissionsToUseApi, () =>
                        {
                            RuleFor(PasswordToken).Must(BeEqualToCustomerPasswordToken).WhenAsync(CustomerIsNotGuest).WithMessage("Invalid token or cancel by refresh token");
                        }).Otherwise(Reject);
                    });
            });
    }

    private static readonly Expression<Func<JwtBearerCustomerAuthenticationContext, JwtBearerCustomerAuthenticationContext>> WholeContext = context => context;

    private static readonly Expression<Func<JwtBearerCustomerAuthenticationContext, Customer>> Customer = context => context.Customer;

    private static readonly Expression<Func<JwtBearerCustomerAuthenticationContext, string>> PasswordToken = context => context.PasswordToken;

    private static bool BeActive(Customer customer) => customer.Active;

    private static bool NotBeDeleted(Customer customer) => !customer.Deleted;

    private async Task<bool> HaveNonNullRefreshToken(JwtBearerCustomerAuthenticationContext context, CancellationToken cancellationToken)
    {
        var refreshToken = await GetRefreshToken(context);
        return refreshToken != null;
    }

    private static bool HaveNonNullProvidedRefreshId(JwtBearerCustomerAuthenticationContext context) => !string.IsNullOrEmpty(context.RefreshId);

    private async Task<bool> HaveProvidedRefreshIdMatchingTheIdOfRefreshToken(JwtBearerCustomerAuthenticationContext context, CancellationToken cancellationToken)
    {
        var refreshToken = await GetRefreshToken(context);
        return context.RefreshId.Equals(refreshToken.RefreshId);
    }

    private static async Task<bool> CustomerHasPermissionsToUseApi(JwtBearerCustomerAuthenticationContext context, CancellationToken cancellationToken)
    {
        return await context.PermissionService.Authorize(StandardPermission.AllowUseApi, context.Customer);
    }

    private static bool BeEqualToCustomerPasswordToken(JwtBearerCustomerAuthenticationContext context, string passwordToken)
    {
        var customerPasswordToken = context.Customer.GetUserFieldFromEntity<string>(SystemCustomerFieldNames.PasswordToken);
        return passwordToken.Equals(customerPasswordToken);
    }

    private async Task<bool> CustomerIsNotGuest(JwtBearerCustomerAuthenticationContext context, CancellationToken cancellationToken) => !await context.GroupService.IsGuest(context.Customer);

    private void Reject() => RuleFor(WholeContext).Must(_ => false).WithMessage("You do not have permission to use API operation (Customer group)");

    private async Task<RefreshToken> GetRefreshToken(JwtBearerCustomerAuthenticationContext context) => _refreshToken ??= await context.RefreshTokenService.GetCustomerRefreshToken(context.Customer);
}